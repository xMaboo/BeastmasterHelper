using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
namespace Beastmaster;

public sealed class ReactionRequests : IDisposable {
	public sealed class Request {
		[JsonIgnore]
		public DateTime Deadline;
		[JsonIgnore]
		public uint SubmittedAction;
		[JsonIgnore]
		public uint PressedAction;
		[JsonIgnore]
		public uint Sequence;
		[JsonIgnore]
		public DateTime ConfirmBy;
		public string Id { get; set; }="";
		public uint ActionId { get; set; }
		public ulong TargetId { get; set; }
		public double TimeoutSeconds { get; set; }=5.0;
		public bool IgnoreWeave { get; set; }
		public string State { get; set; }="Queued";
		public string Reason { get; set; }="Waiting for rotation";
	}
	private readonly Dictionary<string,Request> requests=new Dictionary<string,Request>();
	private readonly ConcurrentQueue<(uint Action,uint Sequence,DateTime At)> effects=new ConcurrentQueue<(uint,uint,DateTime)>();
	private readonly Dictionary<string,uint> aliases=new Dictionary<string,uint>(StringComparer.OrdinalIgnoreCase);
	private readonly uint job;
	private readonly Func<bool> enabled;
	private readonly Func<uint,bool> held;
	private readonly Dictionary<uint,(string Name,string Capability)> actions=new Dictionary<uint,(string,string)>();
	private readonly ICallGateProvider<string> capabilities;
	private readonly ICallGateProvider<string,string> submit;
	private readonly ICallGateProvider<string,string> status;
	private readonly ICallGateProvider<string,bool> cancel;
	private readonly Hook<ActionEffectHandler.Delegates.Receive>? hook;
	private uint territory;
	private ulong actor;
	public event Action<uint,ulong>? Executed;
	public void AddAction(uint id) {
		if(id!=0&&!actions.ContainsKey(id)) {
			string text=DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(id)?.Name.ToString() ?? string.Empty;
			actions[id]=((text.Length>0)?text:$"Action {id}","");
		}
	}
	public void AddAlias(string name,uint id) {
		if(actions.ContainsKey(id)) {
			aliases[name]=id;
		}
	}
	private static bool Terminal(Request r) {
		switch(r.State) {
		case "Executed":
		case "Expired":
		case "Cancelled":
		case "Rejected":
		return true;
		default:
		return false;
		}
	}
	public unsafe ReactionRequests(string prefix,uint job,string abbreviation,Func<bool> enabled,Func<uint,bool> held) {
		ReactionRequests reactionRequests=this;
		this.job=job;
		this.enabled=enabled;
		this.held=held;
		territory=DalamudApi.ClientState.TerritoryType;
		PropertyInfo property=typeof(ClassJobCategory).GetProperty(abbreviation);
		foreach(Lumina.Excel.Sheets.Action item2 in DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()) {
			if(item2.RowId==0||item2.Name.ToString().Length==0) {
				continue;
			}
			object obj=property?.GetValue(item2.ClassJobCategory.Value);
			if(!(obj is bool)||!(bool)obj) {
				continue;
			}
			string text=item2.Name.ToString();
			string text2;
			if((text=="Arm's Length"||text=="Surecast")?true:false) {
				text2="KnockbackImmunity";
			}
			else {
				bool flag;
				switch(text) {
				case "Tactician":
				case "Troubadour":
				case "Shield Samba":
				flag=true;
				break;
				default:
				flag=false;
				break;
				}
				text2=(flag?"PartyMitigation":"");
			}
			string item=text2;
			actions[item2.RowId]=(text,item);
		}
		try {
			hook=DalamudApi.GameInterop.HookFromAddress<ActionEffectHandler.Delegates.Receive>(ActionEffectHandler.Addresses.Receive.Value,Effect);
			hook.Enable();
		}
		catch(Exception exception) {
			DalamudApi.Log.Error(exception,"Reaction request confirmation hook unavailable; requests disabled.");
		}
		IDalamudPluginInterface pluginInterface=DalamudApi.PluginInterface;
		capabilities=pluginInterface.GetIpcProvider<string>(prefix+"ReactionCapabilities");
		capabilities.RegisterFunc(() => JsonConvert.SerializeObject(new {
			Version = 1,
			Jobs = new uint[1] { job },
			Confirmation = (reactionRequests.hook!=null),
			Actions = reactionRequests.actions.Select((KeyValuePair<uint,(string Name,string Capability)> a) => new {
				ActionId = a.Key,
				Name = a.Value.Name,
				Capability = a.Value.Capability,
				Aliases = (from p in reactionRequests.aliases
					where p.Value==a.Key
					select p.Key).ToArray()
			}),
			Targets = new string[2] { "Self", "CurrentTarget" },
			Weaving = new string[2] { "Respect", "Immediate" }
		}));
		submit=pluginInterface.GetIpcProvider<string,string>(prefix+"ReactionSubmit");
		submit.RegisterFunc(Submit);
		status=pluginInterface.GetIpcProvider<string,string>(prefix+"ReactionStatus");
		status.RegisterFunc((string id) => {
			reactionRequests.Maintain();
			return JsonConvert.SerializeObject(reactionRequests.requests.GetValueOrDefault(id) ?? new Request {
				Id = id,
				State = "Rejected",
				Reason = "Unknown request (provider reloaded?)"
			});
		});
		cancel=pluginInterface.GetIpcProvider<string,bool>(prefix+"ReactionCancel");
		cancel.RegisterFunc((string id) => {
			if(!reactionRequests.requests.TryGetValue(id,out Request value)||Terminal(value)) {
				return false;
			}
			value.State="Cancelled";
			value.Reason="Cancelled by owner";
			return true;
		});
	}
	private string Submit(string json) {
		Maintain();
		Request request;
		try {
			request=JsonConvert.DeserializeObject<Request>(json);
		}
		catch {
			request=null;
		}
		if(request==null||string.IsNullOrWhiteSpace(request.Id)||request.Id.Length>128) {
			return "Invalid request";
		}
		if(requests.ContainsKey(request.Id)) {
			return "";
		}
		if(hook==null) {
			return "Execution confirmation unavailable";
		}
		if(Enumerable.Count(requests.Values,(Request r) => !Terminal(r))>=512) {
			return "Request capacity reached";
		}
		if(!actions.ContainsKey(request.ActionId)) {
			return "Action is not supported by this job";
		}
		if(!double.IsFinite(request.TimeoutSeconds)||request.TimeoutSeconds<=0.0||request.TimeoutSeconds>120.0) {
			return "Invalid deadline";
		}
		if(DalamudApi.ObjectTable.LocalPlayer?.ClassJob.RowId!=job) {
			return "Wrong job";
		}
		request.State="Queued";
		request.Reason="Waiting for rotation";
		request.SubmittedAction=0u;
		request.Deadline=DateTime.UtcNow.AddSeconds(request.TimeoutSeconds);
		requests.Add(request.Id,request);
		return "";
	}
	public void Maintain() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		bool flag=territory!=DalamudApi.ClientState.TerritoryType||(actor!=0L&&actor!=localPlayer?.GameObjectId);
		territory=DalamudApi.ClientState.TerritoryType;
		actor=localPlayer?.GameObjectId ?? 0;
		while(true) {
			if(!effects.TryDequeue(out var action)) {
				break;
			}
			Request request=requests.Values.FirstOrDefault((Request r) => r.State=="Submitted"&&(r.SubmittedAction==action.Action||r.PressedAction==action.Action)&&r.Sequence==action.Sequence&&action.At<=r.Deadline);
			if(request!=null) {
				request.State="Executed";
				request.Reason="Confirmed by action effect";
				Executed?.Invoke(request.ActionId,request.TargetId);
			}
		}
		foreach(Request item in requests.Values.Where((Request r) => !Terminal(r))) {
			if(flag||localPlayer==null||localPlayer.IsDead||localPlayer.ClassJob.RowId!=job) {
				item.State="Cancelled";
				item.Reason="Character, territory, job or life state changed";
			}
			else if(DateTime.UtcNow>=item.Deadline) {
				item.Reason=((item.State=="Submitted")?"No execution confirmation before deadline":"Deadline elapsed");
				item.State="Expired";
			}
			else if(item.State=="Submitted"&&DateTime.UtcNow>=item.ConfirmBy) {
				item.State="Rejected";
				item.Reason=$"Game did not confirm execution (pressed {item.PressedAction}, swapped {item.SubmittedAction}, sequence {item.Sequence})";
			}
			else if(!enabled()&&item.State!="Submitted") {
				item.State="Blocked";
				item.Reason="Rotation is disabled";
			}
		}
		string[] array=(from p in requests
			where Terminal(p.Value)&&DateTime.UtcNow>p.Value.Deadline.AddMinutes(2.0)
			select p.Key).ToArray();
		foreach(string key in array) {
			requests.Remove(key);
		}
	}
	public string Describe() {
		Maintain();
		List<string> list=requests.Values.Select((Request r) => $"{actions.GetValueOrDefault(r.ActionId).Name ?? r.ActionId.ToString()} -> {r.State}: {r.Reason}").ToList();
		List<string> list2=new List<string>();
		int num;
		for(num=0;num<list.Count;num++) {
			int num2;
			for(num2=1;num+num2<list.Count&&list[num+num2]==list[num];num2++) {
			}
			list2.Add((num2>1)?$"{list[num]} x{num2}":list[num]);
			num+=num2-1;
		}
		if(list2.Count!=0) {
			return string.Join(" | ",list2.TakeLast(12));
		}
		return "no requests";
	}
	public void Block(string reason) {
		Maintain();
		foreach(Request item in requests.Values.Where((Request r) => !Terminal(r)&&r.State!="Submitted")) {
			item.State="Blocked";
			item.Reason=reason;
		}
	}
	public unsafe bool TryExecute(Func<ulong,bool>? allowedTarget=null,bool allowIdleWeave=true) {
		Maintain();
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		ActionManager* ptr=ActionManager.Instance();
		if(localPlayer==null||ptr==null||!enabled()||localPlayer.ClassJob.RowId!=job||localPlayer.IsDead) {
			return false;
		}
		if(requests.Values.Any((Request r) => r.State=="Submitted")) {
			return true;
		}
		foreach(Request item in requests.Values.Where((Request r) => !Terminal(r))) {
			uint adjustedActionId=ptr->GetAdjustedActionId(item.ActionId);
			uint[] array=((adjustedActionId==item.ActionId)?new uint[1] { item.ActionId }:new uint[2] { item.ActionId, adjustedActionId });
			ulong targetId=item.TargetId;
			if(targetId==0L||(allowedTarget!=null&&!allowedTarget(targetId))) {
				item.State="Blocked";
				item.Reason="Target unavailable or forbidden";
				continue;
			}
			if(held(item.ActionId)||held(adjustedActionId)) {
				item.State="Blocked";
				item.Reason="Action is held";
				continue;
			}
			Lumina.Excel.Sheets.Action row=DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRow(adjustedActionId);
			RecastDetail* recastGroupDetail=ptr->GetRecastGroupDetail(57);
			float num=((recastGroupDetail==null)?0f:Math.Max(0f,recastGroupDetail->Total-recastGroupDetail->Elapsed));
			bool flag=row.CooldownGroup==58||row.AdditionalCooldownGroup==58;
			if(localPlayer.IsCasting||ptr->AnimationLock>0f) {
				goto IL_0244;
			}
			bool num2;
			if(!flag) {
				if(!item.IgnoreWeave) {
					if(num>0f&&num<0.7f) {
						goto IL_0244;
					}
					if(num<=0f) {
						num2=!allowIdleWeave;
						goto IL_0242;
					}
				}
				goto IL_0261;
			}
			num2=num>0f;
			goto IL_0242;
			IL_0244:
			item.State="Waiting";
			item.Reason="Waiting for cast, animation lock or weave window";
			continue;
			IL_0242:
			if(num2) {
				goto IL_0244;
			}
			goto IL_0261;
			IL_0261:
			if((double)(int)row.Cast100ms/10.0>(item.Deadline-DateTime.UtcNow).TotalSeconds) {
				item.State="Blocked";
				item.Reason="Cast cannot finish before deadline";
				continue;
			}
			uint usableId=0u;
			uint num3=0u;
			uint[] array2=array;
			foreach(uint num5 in array2) {
				num3=ptr->GetActionStatus(ActionType.Action,num5,targetId,checkRecastActive: true,checkCastingActive: true,null);
				if(num3!=0&&num3!=565&&ChargesLeft(ptr,num5)>=1) {
					num3=ptr->GetActionStatus(ActionType.Action,num5,targetId,checkRecastActive: false,checkCastingActive: true,null);
				}
				if(num3==0||num3==565) {
					usableId=num5;
					break;
				}
			}
			if(usableId==0) {
				item.State="Blocked";
				item.Reason=$"Action unavailable ({num3})";
				continue;
			}
			foreach(uint item2 in array.Where((uint c) => c==usableId).Concat(array.Where((uint c) => c!=usableId))) {
				if(ptr->UseAction(ActionType.Action,item2,targetId,0u,ActionManager.UseActionMode.None,0u,null)) {
					item.State="Submitted";
					item.Reason="Awaiting action-effect confirmation";
					item.SubmittedAction=adjustedActionId;
					item.PressedAction=item2;
					item.Sequence=ptr->LastUsedActionSequence;
					item.ConfirmBy=DateTime.UtcNow.AddSeconds(Math.Max(3.0,(double)(int)row.Cast100ms/10.0+2.0));
					return true;
				}
			}
			item.State="Blocked";
			item.Reason="Game refused action";
		}
		return false;
	}
	private unsafe static int ChargesLeft(ActionManager* am,uint action) {
		float num=(float)(int)(DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(action)?.Recast100ms ?? 0)/10f;
		float recastTime=am->GetRecastTime(ActionType.Action,action);
		if(num<=0f||recastTime<=0f) {
			return -1;
		}
		int num2=(int)MathF.Round(recastTime/num);
		if(num2<=1) {
			return -1;
		}
		float recastTimeElapsed=am->GetRecastTimeElapsed(ActionType.Action,action);
		if(!(recastTimeElapsed>=recastTime)) {
			return Math.Min(num2,(int)(recastTimeElapsed/num));
		}
		return num2;
	}
	private unsafe void Effect(uint casterId,Character* caster,Vector3* pos,ActionEffectHandler.Header* header,ActionEffectHandler.TargetEffects* hit,GameObjectId* targets) {
		try {
			if(header!=null&&header->ActionType==1&&casterId==DalamudApi.ObjectTable.LocalPlayer?.EntityId) {
				effects.Enqueue((header->ActionId,header->SourceSequence,DateTime.UtcNow));
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Error(exception,"Reaction confirmation failed");
		}
		hook.Original(casterId,caster,pos,header,hit,targets);
	}
	public void Dispose() {
		capabilities.UnregisterFunc();
		submit.UnregisterFunc();
		status.UnregisterFunc();
		cancel.UnregisterFunc();
		hook?.Dispose();
		requests.Clear();
	}
}
