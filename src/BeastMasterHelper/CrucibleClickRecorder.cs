using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

public sealed class CrucibleClickRecorder : IDisposable {
	private readonly List<CrucibleRecordedClick> recorded=new List<CrucibleRecordedClick>();
	private readonly object gate=new object();
	private string watching=string.Empty;
	public const string AllCrucibleWindows="XBM*";
	public const string AnyWindow="*";
	private readonly Dictionary<string,string> windows=new Dictionary<string,string>(StringComparer.Ordinal);
	private const int AnyParam=int.MinValue;
	public static CrucibleClickRecorder Shared { get; }=new CrucibleClickRecorder();
	public string Watching {
		get {
			lock(gate) {
				return watching;
			}
		}
	}
	public bool IsRecording => Watching.Length>0;
	public string Status { get; private set; }="Not recording.";
	public int Count {
		get {
			lock(gate) {
				return recorded.Count;
			}
		}
	}
	public IReadOnlyList<CrucibleRecordedClick> All {
		get {
			lock(gate) {
				return recorded.ToList();
			}
		}
	}
	public CrucibleRecordedClick? LastSelection {
		get {
			lock(gate) {
				for(int num=recorded.Count-1;num>=0;num--) {
					if(IsSelection((AtkEventType)recorded[num].EventType)) {
						return recorded[num];
					}
				}
				return null;
			}
		}
	}
	public void Start(string addonName) {
		Stop();
		if(!string.IsNullOrWhiteSpace(addonName)) {
			lock(gate) {
				watching=addonName;
			}
			DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent,OnEvent);
			Status="Recording "+addonName+". Click one row by hand, then stop.";
		}
	}
	public void Stop() {
		if(IsRecording) {
			DalamudApi.AddonLifecycle.UnregisterListener(OnEvent);
			lock(gate) {
				watching=string.Empty;
			}
			Status=$"Stopped. {Count} event(s) recorded.";
		}
	}
	private bool Matches(string addonName) {
		string text=Watching;
		if(text.Length==0) {
			return false;
		}
		if(text=="*") {
			return true;
		}
		if(!(text=="XBM*")) {
			return string.Equals(addonName,text,StringComparison.Ordinal);
		}
		return addonName.StartsWith("XBM",StringComparison.OrdinalIgnoreCase);
	}
	private void NoteWindow(string addonName) {
		lock(gate) {
			if(windows.ContainsKey(addonName)) {
				return;
			}
			windows[addonName]=string.Empty;
		}
		string value;
		try {
			value=CrucibleUi.DescribeAddon(addonName);
		}
		catch(Exception ex) {
			value="could not read "+addonName+": "+ex.Message;
		}
		lock(gate) {
			windows[addonName]=value;
		}
	}
	public string Report() {
		List<string> list=new List<string> {
			$"=== Crucible input trace, {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===",
			"Watching: "+((Watching.Length==0)?"stopped":Watching),
			string.Empty,
			"=== events, oldest first ==="
		};
		List<CrucibleRecordedClick> list2;
		List<KeyValuePair<string,string>> list3;
		lock(gate) {
			list2=recorded.ToList();
			list3=windows.ToList();
		}
		if(list2.Count==0) {
			list.Add("(nothing recorded - was the trace running when you clicked?)");
		}
		foreach(CrucibleRecordedClick item in list2) {
			list.Add(item.ToString());
			list.Add("    payload: "+Convert.ToBase64String(item.Payload));
		}
		CrucibleRecordedClick lastSelection=LastSelection;
		list.Add(string.Empty);
		list.Add((lastSelection==null)?"No selection-like event recorded yet.":$"last selection: {lastSelection}");
		foreach(var (text3,text4) in list3) {
			list.Add(string.Empty);
			list.Add("=== node tree: "+text3+" ===");
			list.Add((text4.Length==0)?"(not captured)":text4);
		}
		return string.Join(Environment.NewLine,list);
	}
	public void Clear() {
		lock(gate) {
			windows.Clear();
			recorded.Clear();
		}
		Status="Cleared.";
	}
	public CrucibleRecordedClick? LastOfType(AtkEventType type) {
		lock(gate) {
			for(int num=recorded.Count-1;num>=0;num--) {
				if((uint)(byte)recorded[num].EventType==(uint)type) {
					return recorded[num];
				}
			}
			return null;
		}
	}
	private static bool IsNoise(AtkEventType type) {
		switch(type) {
		case AtkEventType.MouseMove:
		case AtkEventType.MouseOver:
		case AtkEventType.MouseOut:
		case AtkEventType.InputNavigation:
		case AtkEventType.FocusStart:
		case AtkEventType.FocusStop:
		case AtkEventType.WindowRollOver:
		case AtkEventType.WindowRollOut:
		return true;
		default:
		return false;
		}
	}
	private static bool IsClick(AtkEventType type) {
		bool flag;
		switch(type) {
		case AtkEventType.MouseDown:
		case AtkEventType.MouseClick:
		case AtkEventType.InputReceived:
		case AtkEventType.ButtonClick:
		case AtkEventType.ListItemClick:
		flag=true;
		break;
		default:
		flag=false;
		break;
		}
		if(!flag) {
			return type.ToString().Contains("Click",StringComparison.Ordinal);
		}
		return true;
	}
	private static bool IsSelection(AtkEventType type) {
		switch(type) {
		case AtkEventType.MouseClick:
		case AtkEventType.InputReceived:
		case AtkEventType.ButtonClick:
		case AtkEventType.ListItemClick:
		return true;
		default:
		return false;
		}
	}
	private unsafe void OnEvent(AddonEvent phase,AddonArgs args) {
		try {
			if(!Matches(args.AddonName)||!(args is AddonReceiveEventArgs e)||(Watching=="*"&&!IsClick((AtkEventType)e.AtkEventType))) {
				return;
			}
			if(args.AddonName.StartsWith("XBM",StringComparison.OrdinalIgnoreCase)) {
				NoteWindow(args.AddonName);
			}
			AtkUnitBase* address=(AtkUnitBase*)args.Addon.Address;
			AtkEvent* atkEvent=(AtkEvent*)e.AtkEvent;
			byte[] array=new byte[sizeof(AtkEventData)];
			AtkEventData* atkEventData=(AtkEventData*)e.AtkEventData;
			if(atkEventData!=null) {
				new ReadOnlySpan<byte>(atkEventData,sizeof(AtkEventData)).CopyTo(array);
			}
			uint targetNodeId=0u;
			string rowText=string.Empty;
			if(address!=null&&atkEvent!=null&&atkEvent->Target!=null) {
				(targetNodeId,rowText)=Locate(address,(AtkResNode*)atkEvent->Target);
			}
			CrucibleRecordedClick item=new CrucibleRecordedClick {
				Addon = args.AddonName,
				EventType = (uint)e.AtkEventType,
				Param = e.EventParam,
				TargetNodeId = targetNodeId,
				RowText = rowText,
				Payload = array
			};
			lock(gate) {
				recorded.Add(item);
				while(recorded.Count>1500) {
					recorded.RemoveAt(0);
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Click recording failed.");
		}
	}
	public unsafe bool Replay(CrucibleRecordedClick click,string? rowText=null) {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(click.Addon).Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			Status=click.Addon+" is not open.";
			return false;
		}
		AtkEventType atkEventType=(AtkEventType)click.EventType;
		AtkEventListener* ptr=(AtkEventListener*)address;
		AtkEvent* ptr2=FindRegistered(&address->UldManager,atkEventType,click.Param,ptr,0);
		if(ptr2==null) {
			Status=$"No registered {atkEventType} param {click.Param} left on {click.Addon}.";
			return false;
		}
		AtkEvent atkEvent=*ptr2;
		atkEvent.Node=null;
		atkEvent.State.StateFlags=AtkEventStateFlags.Unk3;
		atkEvent.State.ReturnFlags=0;
		byte[] payload=click.Payload;
		AtkEventData atkEventData=default;
		fixed(byte* source=payload) {
			Buffer.MemoryCopy(source,&atkEventData,sizeof(AtkEventData),Math.Min(payload.Length,sizeof(AtkEventData)));
		}
		if(!string.IsNullOrWhiteSpace(rowText)&&!Retarget(address,&atkEventData,rowText)) {
			Status="Could not find a row containing "+rowText+" to aim the recording at.";
			return false;
		}
		ptr->ReceiveEvent(atkEventType,click.Param,&atkEvent,&atkEventData);
		Status=$"Replayed {atkEventType} param {click.Param}"+(string.IsNullOrWhiteSpace(rowText)?".":(" at "+rowText+"."));
		return true;
	}
	private unsafe static bool Retarget(AtkUnitBase* unit,AtkEventData* data,string rowText) {
		int index=0;
		AtkComponentListItemRenderer* ptr=FindRow(&unit->UldManager,rowText,0,ref index);
		if(ptr==null) {
			return false;
		}
		data->ListItemData.ListItemRenderer=ptr;
		data->ListItemData.SelectedIndex=index;
		data->ListItemData.HoveredItemIndex3=(short)index;
		return true;
	}
	private unsafe static AtkComponentListItemRenderer* FindRow(AtkUldManager* manager,string text,int depth,ref int index) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return null;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			if(component->GetComponentType()==ComponentType.ListItemRenderer) {
				List<string> list=new List<string>();
				CollectText(&component->UldManager,list,0);
				if(list.Any((string t) => t.Contains(text,StringComparison.OrdinalIgnoreCase))) {
					return (AtkComponentListItemRenderer*)component;
				}
				index++;
			}
			else {
				AtkComponentListItemRenderer* ptr2=FindRow(&component->UldManager,text,depth+1,ref index);
				if(ptr2!=null) {
					return ptr2;
				}
			}
		}
		return null;
	}
	private unsafe static AtkEvent* FindRegistered(AtkUldManager* manager,AtkEventType type,int param,AtkEventListener* listener,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return null;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null) {
				continue;
			}
			AtkEvent* ptr2=ptr->AtkEventManager.Event;
			int num=0;
			while(ptr2!=null&&num<128) {
				if(ptr2->State.EventType==type&&(param==int.MinValue||ptr2->Param==param)&&ptr2->Listener==listener) {
					return ptr2;
				}
				num++;
				ptr2=ptr2->NextEvent;
			}
			if((int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component!=null) {
				AtkEvent* ptr3=FindRegistered(&component->UldManager,type,param,listener,depth+1);
				if(ptr3!=null) {
					return ptr3;
				}
			}
		}
		return null;
	}
	private unsafe static (uint NodeId,string RowText) Locate(AtkUnitBase* unit,AtkResNode* target) {
		return Locate(&unit->UldManager,target,string.Empty,0);
	}
	private unsafe static (uint,string) Locate(AtkUldManager* manager,AtkResNode* target,string row,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return (0u,row);
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null) {
				continue;
			}
			if(ptr==target) {
				return (ptr->NodeId,row);
			}
			if((int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			string text=row;
			if(text.Length==0) {
				List<string> list=new List<string>();
				CollectText(&component->UldManager,list,0);
				text=list.Select((string t) => CrucibleItemCatalogue.ByName(t)?.Name).FirstOrDefault((string name) => name!=null) ?? string.Empty;
			}
			(uint,string) result=Locate(&component->UldManager,target,text,depth+1);
			if(result.Item1!=0) {
				return result;
			}
		}
		return (0u,row);
	}
	private unsafe static void CollectText(AtkUldManager* manager,List<string> texts,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null) {
				continue;
			}
			if(ptr->Type==NodeType.Text) {
				string text=((AtkTextNode*)ptr)->NodeText.ToString();
				if(text.Length>0) {
					texts.Add(text);
				}
			}
			if((int)ptr->Type>=1000) {
				AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
				if(component!=null) {
					CollectText(&component->UldManager,texts,depth+1);
				}
			}
		}
	}
	public string? Serialize() {
		CrucibleRecordedClick lastSelection=LastSelection;
		if(lastSelection!=null) {
			return $"{lastSelection.Addon}|{lastSelection.EventType}|{lastSelection.Param}|{Convert.ToBase64String(lastSelection.Payload)}";
		}
		return null;
	}
	public void Restore(string? stored) {
		if(string.IsNullOrWhiteSpace(stored)) {
			return;
		}
		try {
			string[] array=stored.Split('|');
			if(array.Length==4) {
				CrucibleRecordedClick crucibleRecordedClick=new CrucibleRecordedClick {
					Addon = array[0],
					EventType = uint.Parse(array[1]),
					Param = int.Parse(array[2]),
					TargetNodeId = 0u,
					RowText = string.Empty,
					Payload = Convert.FromBase64String(array[3])
				};
				lock(gate) {
					recorded.Add(crucibleRecordedClick);
				}
				Status=$"Restored a recorded {(AtkEventType)(byte)crucibleRecordedClick.EventType} for {crucibleRecordedClick.Addon}.";
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not restore the recorded click.");
		}
	}
	public unsafe static bool SendButtonClick(string addon,int param,byte[] payload) {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addon).Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			return false;
		}
		AtkEventListener* ptr=(AtkEventListener*)address;
		AtkEvent* ptr2=FindRegistered(&address->UldManager,AtkEventType.ButtonClick,int.MinValue,ptr,0);
		if(ptr2==null) {
			return false;
		}
		AtkEvent atkEvent=*ptr2;
		atkEvent.Node=null;
		atkEvent.Param=(uint)param;
		atkEvent.State.StateFlags=AtkEventStateFlags.Unk3;
		atkEvent.State.ReturnFlags=0;
		AtkEventData atkEventData=default;
		fixed(byte* source=payload) {
			Buffer.MemoryCopy(source,&atkEventData,sizeof(AtkEventData),Math.Min(payload.Length,sizeof(AtkEventData)));
		}
		ptr->ReceiveEvent(AtkEventType.ButtonClick,param,&atkEvent,&atkEventData);
		return true;
	}
	public void Dispose() {
		Stop();
	}
}
