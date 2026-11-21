using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

public sealed class CrucibleDiagnostics : IDisposable {
	private readonly Queue<string> events=new Queue<string>();
	private readonly Queue<string> route=new Queue<string>();
	private readonly Queue<string> markers=new Queue<string>();
	private readonly object gate=new object();
	private DateTime nextSample;
	private DateTime nextFightSample;
	private string previousFightState="";
	public bool WatchFightPreparation { get; set; }
	public string FightSnapshot { get; private set; }="Step onto a fight pad, then capture both windows.";
	public string ItemSnapshot { get; private set; }="Capture before and after using a HUD item, in and out of combat.";
	public bool RecordingEvents { get; private set; }
	public bool RecordingRoute { get; private set; }
	public string LastSnapshot { get; private set; }="No snapshot captured.";
	public string ExportStatus { get; private set; }="";
	public int EventCount {
		get {
			lock(gate) {
				return events.Count;
			}
		}
	}
	public string EventPreview {
		get {
			lock(gate) {
				return string.Join("\n",events.TakeLast(25));
			}
		}
	}
	public int MarkerCount {
		get {
			lock(gate) {
				return markers.Count;
			}
		}
	}
	public int RouteCount {
		get {
			lock(gate) {
				return route.Count;
			}
		}
	}
	public void CaptureItems(string label) {
		try {
			ItemSnapshot=$"{DateTime.UtcNow:O} HUD/LOOT {label} combat={DalamudApi.Condition[ConditionFlag.InCombat]} territory={DalamudApi.ClientState.TerritoryType}\n"+"Crucible Items capacity reported by user: 10 slots. Beast gear has SEPARATE storage and is passive, never usable. Occupancy/quantities are not yet mapped. Recognised names are not slot counts.\n"+CrucibleContents.Describe()+"\nHOTBAR COMMANDS\n"+CrucibleHotbarDiagnostics.Describe()+"\nHUD CONTROLS\n"+CrucibleUi.DescribeAddon("XBMContentsMainHUD")+"\nLOOT CONTROLS\n"+CrucibleUi.DescribeAddon("XBMContentsBooty")+"\nCONTEXT MENU\n"+CrucibleUi.DescribeAddon("ContextMenu")+"\nICON CONTEXT MENU\n"+CrucibleUi.DescribeAddon("ContextIconMenu")+"\nFOLLOW-UP CHOICE\n"+CrucibleUi.DescribeAddon("SelectString")+"\nCONFIRMATION\n"+CrucibleUi.DescribeAddon("SelectYesno");
			lock(gate) {
				markers.Enqueue(ItemSnapshot);
				while(markers.Count>120) {
					markers.Dequeue();
				}
			}
		}
		catch(Exception ex) {
			ItemSnapshot="Item capture failed: "+ex.Message;
		}
	}
	public void CaptureFightPreparation() {
		string state=CrucibleUi.DescribeFightPreparation();
		StoreFightPreparation(state);
	}
	private void StoreFightPreparation(string state) {
		previousFightState=state;
		FightSnapshot=state;
		string value=string.Join("\n",BeastmasterCatalog.Entries.Select((BeastmasterCatalogEntry e) => $"catalogue={e.Number} name={e.DisplayName} attribute={e.Attribute}"));
		string item=$"{DateTime.UtcNow:O} FIGHT PREPARATION\n{state}\nCATALOGUE REFERENCE (not live availability or selected team)\n{value}";
		lock(gate) {
			markers.Enqueue(item);
			while(markers.Count>120) {
				markers.Dequeue();
			}
		}
	}
	public CrucibleDiagnostics() {
		DalamudApi.Framework.Update+=Update;
	}
	public void SetEventRecording(bool enabled) {
		if(enabled!=RecordingEvents) {
			if(enabled) {
				DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent,OnEvent);
				DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent,OnEvent);
			}
			else {
				DalamudApi.AddonLifecycle.UnregisterListener(OnEvent);
			}
			RecordingEvents=enabled;
		}
	}
	private unsafe void OnEvent(AddonEvent phase,AddonArgs args) {
		bool flag=!RecordingEvents;
		if(!flag) {
			bool flag2=!args.AddonName.StartsWith("XBM",StringComparison.OrdinalIgnoreCase);
			if(flag2) {
				bool flag3;
				switch(args.AddonName) {
				case "ContextMenu":
				case "ContextIconMenu":
				case "SelectString":
				case "SelectYesno":
				flag3=true;
				break;
				default:
				flag3=false;
				break;
				}
				flag2=!flag3;
			}
			flag=flag2;
		}
		if(flag||!(args is AddonReceiveEventArgs e)) {
			return;
		}
		try {
			StringBuilder stringBuilder=new StringBuilder($"{DateTime.UtcNow:O} {phase} {args.AddonName} type={e.AtkEventType} ({(int)e.AtkEventType}) param={e.EventParam}");
			AtkEvent* atkEvent=(AtkEvent*)e.AtkEvent;
			if(atkEvent!=null) {
				StringBuilder stringBuilder2=stringBuilder;
				StringBuilder stringBuilder3=stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(37,4,stringBuilder2);
				handler.AppendLiteral(" node=0x");
				handler.AppendFormatted((nuint)atkEvent->Node,"X");
				handler.AppendLiteral(" target=0x");
				handler.AppendFormatted((nuint)atkEvent->Target,"X");
				handler.AppendLiteral(" listener=0x");
				handler.AppendFormatted((nuint)atkEvent->Listener,"X");
				handler.AppendLiteral(" flags=");
				handler.AppendFormatted(atkEvent->State.StateFlags);
				stringBuilder3.Append(ref handler);
			}
			AtkEventData* atkEventData=(AtkEventData*)e.AtkEventData;
			if(atkEventData!=null) {
				StringBuilder stringBuilder2=stringBuilder;
				StringBuilder stringBuilder4=stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(9,1,stringBuilder2);
				handler.AppendLiteral(" payload=");
				handler.AppendFormatted(Convert.ToHexString(new ReadOnlySpan<byte>(atkEventData,sizeof(AtkEventData))));
				stringBuilder4.Append(ref handler);
				int atkEventType=(int)e.AtkEventType;
				if(atkEventType>=31&&atkEventType<=39) {
					stringBuilder2=stringBuilder;
					StringBuilder stringBuilder5=stringBuilder2;
					handler=new StringBuilder.AppendInterpolatedStringHandler(33,4,stringBuilder2);
					handler.AppendLiteral(" row=");
					handler.AppendFormatted(atkEventData->ListItemData.SelectedIndex);
					handler.AppendLiteral(" hover3=");
					handler.AppendFormatted(atkEventData->ListItemData.HoveredItemIndex3);
					handler.AppendLiteral(" renderer=0x");
					handler.AppendFormatted((nuint)atkEventData->ListItemData.ListItemRenderer,"X");
					handler.AppendLiteral(" button=");
					handler.AppendFormatted(atkEventData->ListItemData.MouseButtonId);
					stringBuilder5.Append(ref handler);
				}
				else if(atkEventType>=3&&atkEventType<=10) {
					stringBuilder2=stringBuilder;
					StringBuilder stringBuilder6=stringBuilder2;
					handler=new StringBuilder.AppendInterpolatedStringHandler(18,3,stringBuilder2);
					handler.AppendLiteral(" mouse=(");
					handler.AppendFormatted(atkEventData->MouseData.PosX);
					handler.AppendLiteral(",");
					handler.AppendFormatted(atkEventData->MouseData.PosY);
					handler.AppendLiteral(") button=");
					handler.AppendFormatted(atkEventData->MouseData.ButtonId);
					stringBuilder6.Append(ref handler);
				}
			}
			lock(gate) {
				events.Enqueue(stringBuilder.ToString());
				while(events.Count>500) {
					events.Dequeue();
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Error(exception,"Crucible diagnostic event capture failed");
		}
	}
	public void SetRouteRecording(bool enabled) {
		RecordingRoute=enabled;
		nextSample=DateTime.MinValue;
	}
	private void Update(IFramework framework) {
		if(WatchFightPreparation&&DateTime.UtcNow>=nextFightSample) {
			nextFightSample=DateTime.UtcNow.AddSeconds(2.0);
			try {
				string text=CrucibleUi.DescribeFightPreparation();
				if(text!=previousFightState) {
					StoreFightPreparation(text);
				}
			}
			catch(Exception exception) {
				WatchFightPreparation=false;
				DalamudApi.Log.Error(exception,"Fight preparation capture stopped");
			}
		}
		if(!RecordingRoute||DateTime.UtcNow<nextSample) {
			return;
		}
		nextSample=DateTime.UtcNow.AddSeconds(2.0);
		if(!DalamudApi.Condition[ConditionFlag.BoundByDuty]||DalamudApi.ObjectTable.LocalPlayer==null) {
			return;
		}
		try {
			AddRoute(WorldSnapshot("Route sample"));
		}
		catch(Exception exception2) {
			RecordingRoute=false;
			DalamudApi.Log.Error(exception2,"Crucible route capture stopped");
		}
	}
	private static string WorldSnapshot(string label) {
		IPlayerCharacter player=DalamudApi.ObjectTable.LocalPlayer;
		IGameObject target=DalamudApi.TargetManager.Target;
		StringBuilder stringBuilder=new StringBuilder();
		StringBuilder stringBuilder2=stringBuilder;
		StringBuilder stringBuilder3=stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(8,3,stringBuilder2);
		handler.AppendFormatted(DateTime.UtcNow,"O");
		handler.AppendLiteral(" ");
		handler.AppendFormatted(label);
		handler.AppendLiteral(" | BMH ");
		handler.AppendFormatted(typeof(CrucibleDiagnostics).Assembly.GetName().Version);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder2=stringBuilder;
		StringBuilder stringBuilder4=stringBuilder2;
		handler=new StringBuilder.AppendInterpolatedStringHandler(29,4,stringBuilder2);
		handler.AppendLiteral("territory=");
		handler.AppendFormatted(DalamudApi.ClientState.TerritoryType);
		handler.AppendLiteral(" map=");
		handler.AppendFormatted(DalamudApi.ClientState.MapId);
		handler.AppendLiteral(" duty=");
		handler.AppendFormatted(DalamudApi.Condition[ConditionFlag.BoundByDuty]);
		handler.AppendLiteral(" combat=");
		handler.AppendFormatted(DalamudApi.Condition[ConditionFlag.InCombat]);
		stringBuilder4.AppendLine(ref handler);
		if(player==null) {
			return stringBuilder.AppendLine("Player unavailable.").ToString();
		}
		stringBuilder2=stringBuilder;
		StringBuilder stringBuilder5=stringBuilder2;
		handler=new StringBuilder.AppendInterpolatedStringHandler(29,3,stringBuilder2);
		handler.AppendLiteral("player XYZ=");
		handler.AppendFormatted(player.Position);
		handler.AppendLiteral(" rotation=");
		handler.AppendFormatted(player.Rotation);
		handler.AppendLiteral(" target=");
		handler.AppendFormatted((target==null)?"none":$"{target.Name} data={target.BaseId} entity={target.EntityId:X} XYZ={target.Position}");
		stringBuilder5.AppendLine(ref handler);
		foreach(IGameObject item in (from o in DalamudApi.ObjectTable
			where !(o is IPlayerCharacter)
			orderby Vector3.DistanceSquared(player.Position,o.Position)
			select o).Take(200)) {
			stringBuilder2=stringBuilder;
			StringBuilder stringBuilder6=stringBuilder2;
			handler=new StringBuilder.AppendInterpolatedStringHandler(69,8,stringBuilder2);
			handler.AppendLiteral("object name=");
			handler.AppendFormatted(item.Name);
			handler.AppendLiteral(" kind=");
			handler.AppendFormatted(item.ObjectKind);
			handler.AppendLiteral(" data=");
			handler.AppendFormatted(item.BaseId);
			handler.AppendLiteral(" entity=");
			handler.AppendFormatted(item.EntityId,"X");
			handler.AppendLiteral(" targetable=");
			handler.AppendFormatted(item.IsTargetable);
			handler.AppendLiteral(" XYZ=");
			handler.AppendFormatted(item.Position);
			handler.AppendLiteral(" rotation=");
			handler.AppendFormatted(item.Rotation);
			handler.AppendLiteral(" distance=");
			handler.AppendFormatted(Vector3.Distance(player.Position,item.Position),"F2");
			stringBuilder6.AppendLine(ref handler);
		}
		return stringBuilder.ToString();
	}
	public void Capture(string label) {
		try {
			LastSnapshot=WorldSnapshot(label)+"\nWINDOW STATE\n"+CrucibleUi.Describe();
			lock(gate) {
				markers.Enqueue((LastSnapshot.Length>100000)?(LastSnapshot.Substring(0,100000)+"\n[truncated]"):LastSnapshot);
				while(markers.Count>120) {
					markers.Dequeue();
				}
			}
		}
		catch(Exception ex) {
			LastSnapshot="Capture failed: "+ex.Message;
			DalamudApi.Log.Error(ex,"Crucible snapshot failed");
		}
	}
	private void AddRoute(string sample) {
		lock(gate) {
			route.Enqueue((sample.Length>100000)?(sample.Substring(0,100000)+"\n[truncated]"):sample);
			while(route.Count>120) {
				route.Dequeue();
			}
		}
	}
	public string Report() {
		lock(gate) {
			return "CRUCIBLE DIAGNOSTICS\nEvents: newest 500; route samples: newest 120; labelled snapshots: newest 120; world coordinates are X/Y/Z.\nFAMILIAR ABILITIES\n"+FamiliarCombatData.DescribeAll()+"\nEVENT TRACE\n"+string.Join("\n",events)+"\nLABELLED SNAPSHOTS\n"+string.Join("\n",markers)+"\nROUTE SAMPLES\n"+string.Join("\n",route);
		}
	}
	public void Save() {
		try {
			string text=Path.Combine(DalamudApi.PluginInterface.GetPluginConfigDirectory(),"CrucibleDiagnostics");
			Directory.CreateDirectory(text);
			string text2=Path.Combine(text,$"crucible-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.txt");
			File.WriteAllText(text2,Report());
			ExportStatus="Saved: "+text2;
		}
		catch(Exception ex) {
			ExportStatus="Save failed: "+ex.Message;
		}
	}
	public void Clear() {
		lock(gate) {
			events.Clear();
			route.Clear();
			markers.Clear();
		}
		LastSnapshot="No snapshot captured.";
	}
	public void Dispose() {
		SetEventRecording(enabled: false);
		DalamudApi.Framework.Update-=Update;
	}
}
