using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.Inventory;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Plugin;
using Lumina.Excel.Sheets;
using Microsoft.Win32;
namespace Beastmaster;

public sealed class PluginUI {
	private enum CrucibleMode {
		None,
		Recommended,
		LevelUp,
		Suggested
	}
	private sealed record QuickToggle(string Key,string Label,string DefaultPanel,Func<bool> Get,Func<string> Tip,System.Action Click,Func<bool>? Enabled=null);
	private float arenaNudgeStep=1f;
	private bool resetBoardOptionsConfirming;
	private string crucibleMarker="Entrance";
	private string crucibleBoardDump="Enter a board and press Capture while a Crucible window is open.";
	private int crucibleRecordAddon;
	private static readonly string[] CrucibleRecordableAddons=new string[7] { "XBMContentsItemShop", "XBMContentsTreasure", "XBMContentsBooty", "XBMContentsMainHUD", "XBMPetParty", "XBMMonsterNotebook", "*" };
	private static readonly string[] CrucibleRecordableLabels=CrucibleRecordableAddons.Select((string a) => (!(a=="*"))?a:"ALL windows (any window, clicks only)").ToArray();
	private string crucibleBoardDumpPath=string.Empty;
	private string crucibleInspection="Open the board and team windows, then inspect their visible controls.";
	private int crucibleFallbackCatalogueId=1;
	private int crucibleFallbackRank=1;
	private int levelDebugId=1;
	private string crucibleTracePath=string.Empty;
	private static readonly (string Key,string Label)[] MainSections=new (string,string)[9] {
		("quests","Beastmaster quest chain"),
		("catalog","Beast catalogue"),
		("equipment","Recommended gear"),
		("commands","Commands"),
		("auto-output","Rotation Stuff"),
		("settings","Settings"),
		("debug","DEBUG"),
		("support","Issue Support & Ko-Fi/Tips"),
		("boards","Crucible Board(s) Options")
	};
	private readonly BeastmasterCrucibleService crucibleService;
	private readonly CrucibleLevelDebugger levelDebugger;
	private readonly CrucibleBoardService crucibleBoardService;
	private readonly BeastmasterConfiguration configuration;
	private readonly BeastmasterProgressService progressService;
	private readonly BeastmasterQuestService questService;
	private readonly BeastmasterNavigationService navigationService;
	private readonly BeastmasterDebugDataService debugDataService;
	private readonly BeastmasterAutoCaptureService autoCaptureService;
	private readonly BeastmasterRotationRunner rotationRunner;
	private string debugQuery="Beastmaster";
	private string debugResult="Press a button to read data from the client.";
	private DateTime nextQuestStatusRefreshUtc=DateTime.MinValue;
	private DateTime nextGaugeRefreshUtc=DateTime.MinValue;
	private BeastmasterGaugeSnapshot gaugeSnapshot=BeastmasterGaugeSnapshot.Unavailable("Waiting to read");
	private bool autoOutputCollapsed;
	private int selectedEquipmentSet;
	private DateTime nextEquipmentRefreshUtc=DateTime.MinValue;
	private readonly Dictionary<string,(uint ItemId,uint EquippedCount,uint InventoryCount,uint ArmoryCount)> equipmentOwnership=new Dictionary<string,(uint,uint,uint,uint)>(StringComparer.Ordinal);
	private bool isMainWindowOpen;
	private const string SupportUrl="https://github.com/t3knical/DalamudPlugins/issues";
	private const string KofiUrl="https://ko-fi.com/t3knical";
	private bool recommendedDpsConfirming;
	private string rotationSection="Core";
	private static readonly string[] RotationSections=new string[5] { "Core", "Parting Blow & Cycling", "Shield Charge", "Familiar Selection", "Advanced" };
	private const float RotationControlX=260f;
	private static readonly Vector4 OverlayGold=new Vector4(1f,0.85f,0.3f,1f);
	private static readonly Vector4 OverlayGreen=new Vector4(0.35f,0.9f,0.5f,1f);
	private static readonly Vector4 OverlayGrey=new Vector4(0.6f,0.6f,0.6f,1f);
	private static readonly Vector4 OverlayRed=new Vector4(1f,0.45f,0.4f,1f);
	private static readonly Vector4 OverlayBlue=new Vector4(0.4f,0.75f,1f,1f);
	private static readonly string[] BattlehornLabels=new string[4] { "Auto", "First Battlehorn", "Second Battlehorn", "Third Battlehorn" };
	private bool overlayCollapsed;
	private List<QuickToggle>? quickToggles;
	private string? dragging;
	private string? capturing;
	private readonly HashSet<VirtualKey> peak=new HashSet<VirtualKey>();
	private readonly Dictionary<string,bool> wasDown=new Dictionary<string,bool>();
	private static readonly HashSet<VirtualKey> Modifiers=new HashSet<VirtualKey> {
		VirtualKey.SHIFT,
		VirtualKey.CONTROL,
		VirtualKey.MENU
	};
	private string toggleSettingsSection="Customize";
	private string hotbarSettingsSection="Customize";
	private static readonly Vector4 ToggleOn=new Vector4(0.24f,0.52f,0.26f,0.95f);
	private static readonly Vector4 ToggleOnHover=new Vector4(0.31f,0.65f,0.33f,1f);
	private static readonly Vector4 ToggleOff=new Vector4(0.17f,0.18f,0.19f,0.95f);
	private static readonly Vector4 ToggleOffHover=new Vector4(0.26f,0.27f,0.29f,1f);
	private static readonly Vector4 SlotReady=new Vector4(1f,1f,1f,1f);
	private static readonly Vector4 SlotCooling=new Vector4(0.45f,0.45f,0.5f,1f);
	private static readonly Vector4 SlotLocked=new Vector4(0.3f,0.3f,0.3f,0.45f);
	private static readonly Vector4 SlotDisabled=new Vector4(0.32f,0.22f,0.22f,0.4f);
	private int gridColumn;
	private float gridButtonWidth;
	private int gridColumns=3;
	private List<QuickToggle> QuickToggles => quickToggles ?? (quickToggles=BuildQuickToggles());
	private void DrawCaptureDiagnostics() {
		ImGui.Text("Capture diagnostics");
		ImGui.Separator();
		if(ImGui.Button("Copy target info##BmhCopyTarget")) {
			IBattleChara battleChara=DalamudApi.TargetManager.Target as IBattleChara;
			IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
			ImGui.SetClipboardText((battleChara==null||localPlayer==null)?"No battle target selected.":BeastmasterCaptureTargeting.Describe(battleChara,localPlayer.Level,(ushort)DalamudApi.ClientState.TerritoryType));
		}
		ImGui.SameLine();
		ImGui.TextDisabled("Select the creature first, then paste the result to whoever is debugging it.");
		IPlayerCharacter localPlayer2=DalamudApi.ObjectTable.LocalPlayer;
		ImU8String text=new ImU8String(35,2);
		text.AppendLiteral("Capture enabled: ");
		text.AppendFormatted(autoCaptureService.TryCapture);
		text.AppendLiteral("   ");
		text.AppendLiteral("HP threshold: ");
		text.AppendFormatted(configuration.CaptureHpThreshold,"0");
		text.AppendLiteral("%");
		ImGui.TextDisabled(text);
		int num=BeastmasterTeam.UnlockedCount();
		bool hasValue=BeastmasterTeam.IsUnlocked(1).HasValue;
		ImU8String text2=new ImU8String(45,2);
		text2.AppendLiteral("Bestiary count from client: ");
		text2.AppendFormatted((num<0)?"unreadable":num.ToString());
		text2.AppendLiteral("   mask trusted: ");
		text2.AppendFormatted(hasValue);
		ImGui.TextDisabled(text2);
		if(!hasValue) {
			ImGui.TextColored(new Vector4(1f,0.8f,0.4f,1f),"Unlock mask failed its sanity check, so it is ignored - captures are not blocked by it.");
		}
		if(!(DalamudApi.TargetManager.Target is IBattleChara battleChara2)) {
			ImGui.TextDisabled("No target.");
			ImGui.Spacing();
			return;
		}
		float value=((battleChara2.MaxHp==0)?100f:((float)battleChara2.CurrentHp*100f/(float)battleChara2.MaxHp));
		ImU8String text3=new ImU8String(18,3);
		text3.AppendLiteral("Target: ");
		text3.AppendFormatted(battleChara2.Name.TextValue);
		text3.AppendLiteral("  lv");
		text3.AppendFormatted(battleChara2.Level);
		text3.AppendLiteral("  ");
		text3.AppendFormatted(value,"0.#");
		text3.AppendLiteral("% HP");
		ImGui.TextDisabled(text3);
		bool flag=BeastmasterCaptureAttempts.AlreadyTried(battleChara2.GameObjectId);
		ImU8String text4=new ImU8String(65,2);
		text4.AppendLiteral("Capture already used on this creature: ");
		text4.AppendFormatted(flag);
		text4.AppendLiteral("   ");
		text4.AppendLiteral("(tracking ");
		text4.AppendFormatted(BeastmasterCaptureAttempts.Count);
		text4.AppendLiteral(" creature(s))");
		ImGui.TextDisabled(text4);
		if(flag) {
			ImGui.SameLine();
			if(ImGui.SmallButton("Allow again")) {
				BeastmasterCaptureAttempts.Clear();
			}
		}
		CaptureVerdict captureVerdict=BeastmasterCaptureTargeting.Evaluate(battleChara2,localPlayer2?.Level ?? 1,(ushort)DalamudApi.ClientState.TerritoryType,progressService);
		ImGui.TextColored(captureVerdict.ShouldCapture?new Vector4(0.4f,0.9f,0.5f,1f):new Vector4(1f,0.8f,0.4f,1f),captureVerdict.ShouldCapture?"Capturable: YES":("Capturable: no - "+captureVerdict.Reason));
		bool flag2=false;
		foreach(IStatus status in battleChara2.StatusList) {
			uint statusId=status.StatusId;
			if((statusId==4624||statusId==4626)?true:false) {
				flag2=true;
				bool value2=localPlayer2!=null&&(status.SourceId==localPlayer2.EntityId||status.SourceId==localPlayer2.GameObjectId);
				ImU8String text5=new ImU8String(58,5);
				text5.AppendLiteral("   status ");
				text5.AppendFormatted(status.StatusId);
				text5.AppendLiteral(" source=");
				text5.AppendFormatted(status.SourceId);
				text5.AppendLiteral(" ");
				text5.AppendLiteral("(mine=");
				text5.AppendFormatted(value2);
				text5.AppendLiteral(", my EntityId=");
				text5.AppendFormatted(localPlayer2?.EntityId);
				text5.AppendLiteral(", my GameObjectId=");
				text5.AppendFormatted(localPlayer2?.GameObjectId);
				text5.AppendLiteral(")");
				ImGui.TextDisabled(text5);
			}
		}
		if(!flag2) {
			ImGui.TextDisabled("   no capture status on this target");
		}
		ImGui.Spacing();
	}
	private void DrawArenaSurvey() {
		ImGui.Text("Arena boundary");
		ImGui.TextDisabled("Draw a boundary in the world to measure a board, then copy the numbers out.");
		ImGui.Separator();
		if(ImGui.Checkbox("Draw boundary",ref configuration.ArenaOverlayEnabled)) {
			configuration.Save();
		}
		DrawArenaShapeControls();
		ImGui.Separator();
		DrawArenaCentreControls();
		ImGui.Separator();
		DrawArenaAppearance();
		ImGui.Separator();
		DrawArenaReadout();
	}
	private void DrawArenaShapeControls() {
		int v=(int)configuration.ArenaShape;
		ImGui.Text("Shape");
		ImGui.SameLine();
		if(ImGui.RadioButton("Circle",ref v,0)) {
			configuration.ArenaShape=ArenaShape.Circle;
			configuration.Save();
		}
		ImGui.SameLine();
		if(ImGui.RadioButton("Square",ref v,1)) {
			configuration.ArenaShape=ArenaShape.Square;
			configuration.Save();
		}
		ImGui.SameLine();
		if(ImGui.RadioButton("Rectangle",ref v,2)) {
			if(configuration.ArenaShape!=ArenaShape.Rectangle) {
				configuration.ArenaSizeZ=configuration.ArenaSize;
			}
			configuration.ArenaShape=ArenaShape.Rectangle;
			configuration.Save();
		}
		float v2=configuration.ArenaSize;
		string text=configuration.ArenaShape switch {
			ArenaShape.Circle => "Radius",
			ArenaShape.Rectangle => "Half-width (X)",
			_ => "Half-width",
		};
		ImGui.SetNextItemWidth(220f);
		if(ImGui.SliderFloat(text,ref v2,1f,200f,"%.1fy")) {
			configuration.ArenaSize=v2;
			configuration.Save();
		}
		SettingTip("Distance from the centre to the edge, for both shapes - so switching shape keeps the same meaning. A square's full width is twice this.");
		ImGui.SameLine();
		if(ImGui.SmallButton("-##size")) {
			configuration.ArenaSize=Math.Max(1f,configuration.ArenaSize-arenaNudgeStep);
			configuration.Save();
		}
		ImGui.SameLine();
		if(ImGui.SmallButton("+##size")) {
			configuration.ArenaSize=Math.Min(200f,configuration.ArenaSize+arenaNudgeStep);
			configuration.Save();
		}
		if(configuration.ArenaShape==ArenaShape.Rectangle) {
			float v3=configuration.ArenaSizeZ;
			ImGui.SetNextItemWidth(220f);
			if(ImGui.SliderFloat("Half-depth (Z)",ref v3,1f,200f,"%.1fy")) {
				configuration.ArenaSizeZ=v3;
				configuration.Save();
			}
			ImGui.SameLine();
			if(ImGui.SmallButton("-##depth")) {
				configuration.ArenaSizeZ=Math.Max(1f,configuration.ArenaSizeZ-arenaNudgeStep);
				configuration.Save();
			}
			ImGui.SameLine();
			if(ImGui.SmallButton("+##depth")) {
				configuration.ArenaSizeZ=Math.Min(200f,configuration.ArenaSizeZ+arenaNudgeStep);
				configuration.Save();
			}
		}
		ArenaShape arenaShape=configuration.ArenaShape;
		if((uint)(arenaShape-1)<=1u) {
			float v4=configuration.ArenaRotationDegrees;
			ImGui.SetNextItemWidth(220f);
			float vMax=((configuration.ArenaShape==ArenaShape.Square)?90f:180f);
			if(ImGui.SliderFloat("Rotation",ref v4,0f,vMax,"%.0f deg")) {
				configuration.ArenaRotationDegrees=v4;
				configuration.Save();
			}
			SettingTip((configuration.ArenaShape==ArenaShape.Square)?"For boards that are not axis-aligned. 0-90 covers every orientation, since a square repeats every quarter turn.":"For boards that are not axis-aligned. A rectangle needs 0-180, since it only repeats every half turn.");
		}
	}
	private void DrawArenaCentreControls() {
		int v=(int)configuration.ArenaCentreMode;
		ImGui.Text("Centre");
		ImGui.SameLine();
		if(ImGui.RadioButton("Follow me",ref v,0)) {
			configuration.ArenaCentreMode=ArenaCentreMode.Player;
			configuration.Save();
		}
		ImGui.SameLine();
		if(ImGui.RadioButton("Fixed point",ref v,1)) {
			configuration.ArenaCentreMode=ArenaCentreMode.Fixed;
			configuration.Save();
		}
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		ImGui.BeginDisabled(localPlayer==null);
		if(ImGui.Button("Set centre here")&&localPlayer!=null) {
			configuration.ArenaCentreX=localPlayer.Position.X;
			configuration.ArenaCentreY=localPlayer.Position.Y;
			configuration.ArenaCentreZ=localPlayer.Position.Z;
			configuration.ArenaCentreTerritory=(ushort)DalamudApi.ClientState.TerritoryType;
			configuration.ArenaCentreMode=ArenaCentreMode.Fixed;
			configuration.Save();
		}
		ImGui.EndDisabled();
		SettingTip("Capture your current position as the centre and switch to Fixed. A starting point - nudge it from there rather than walking to the middle.");
		if(configuration.ArenaCentreMode!=ArenaCentreMode.Fixed) {
			ImGui.TextDisabled("Switch to Fixed point to position and nudge the boundary.");
			return;
		}
		DrawArenaNudge();
		DrawArenaCentreFields();
		DrawArenaDistanceReadout(localPlayer);
	}
	private void DrawArenaNudge() {
		ImGui.TextDisabled("Move the boundary");
		ImGui.SetNextItemWidth(90f);
		if(ImGui.InputFloat("Step",ref arenaNudgeStep,0.5f,1f,"%.1fy")) {
			arenaNudgeStep=Math.Clamp(arenaNudgeStep,0.1f,20f);
		}
		SettingTip("How far each nudge moves the centre. Also used by the size +/- buttons.");
		ImGui.Dummy(new Vector2(30f,0f));
		ImGui.SameLine();
		if(ImGui.Button("N##arena",new Vector2(30f,0f))) {
			Nudge(0f,0f-arenaNudgeStep);
		}
		if(ImGui.Button("W##arena",new Vector2(30f,0f))) {
			Nudge(0f-arenaNudgeStep,0f);
		}
		ImGui.SameLine();
		if(ImGui.Button("o##arena",new Vector2(30f,0f))) {
			IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
			if(localPlayer!=null) {
				configuration.ArenaCentreX=localPlayer.Position.X;
				configuration.ArenaCentreY=localPlayer.Position.Y;
				configuration.ArenaCentreZ=localPlayer.Position.Z;
				configuration.Save();
			}
		}
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Snap the centre back to where you are standing.");
		}
		ImGui.SameLine();
		if(ImGui.Button("E##arena",new Vector2(30f,0f))) {
			Nudge(arenaNudgeStep,0f);
		}
		ImGui.Dummy(new Vector2(30f,0f));
		ImGui.SameLine();
		if(ImGui.Button("S##arena",new Vector2(30f,0f))) {
			Nudge(0f,arenaNudgeStep);
		}
		ImGui.SameLine();
		if(ImGui.Button("Up##arena",new Vector2(38f,0f))) {
			configuration.ArenaCentreY+=arenaNudgeStep;
			configuration.Save();
		}
		ImGui.SameLine();
		if(ImGui.Button("Dn##arena",new Vector2(38f,0f))) {
			configuration.ArenaCentreY-=arenaNudgeStep;
			configuration.Save();
		}
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Height only affects where the line is drawn, not the measurement.");
		}
		void Nudge(float dx,float dz) {
			configuration.ArenaCentreX+=dx;
			configuration.ArenaCentreZ+=dz;
			configuration.Save();
		}
	}
	private void DrawArenaCentreFields() {
		Vector3 data=new Vector3(configuration.ArenaCentreX,configuration.ArenaCentreY,configuration.ArenaCentreZ);
		ImGui.SetNextItemWidth(240f);
		if(ImGui.InputFloat3("X / Y / Z",ref data)) {
			configuration.ArenaCentreX=data.X;
			configuration.ArenaCentreY=data.Y;
			configuration.ArenaCentreZ=data.Z;
			configuration.Save();
		}
	}
	private void DrawArenaDistanceReadout(IGameObject? player) {
		ushort num=(ushort)DalamudApi.ClientState.TerritoryType;
		if(configuration.ArenaCentreTerritory!=0&&configuration.ArenaCentreTerritory!=num) {
			Vector4 col=new Vector4(1f,0.8f,0.4f,1f);
			ImU8String text=new ImU8String(46,2);
			text.AppendLiteral("Centre was captured in territory ");
			text.AppendFormatted(configuration.ArenaCentreTerritory);
			text.AppendLiteral(", you are in ");
			text.AppendFormatted(num);
			ImGui.TextColored(in col,text);
		}
		else {
			if(player==null) {
				return;
			}
			float num2=player.Position.X-configuration.ArenaCentreX;
			float num3=player.Position.Z-configuration.ArenaCentreZ;
			float value=MathF.Sqrt(num2*num2+num3*num3);
			Vector4 col=new Vector4(0.4f,0.9f,0.5f,1f);
			ImU8String text2=new ImU8String(33,3);
			text2.AppendLiteral("You are ");
			text2.AppendFormatted(value,"0.0");
			text2.AppendLiteral("y from centre  (dx ");
			text2.AppendFormatted(num2,"+0.0;-0.0");
			text2.AppendLiteral(", dz ");
			text2.AppendFormatted(num3,"+0.0;-0.0");
			text2.AppendLiteral(")");
			ImGui.TextColored(in col,text2);
			if(configuration.ArenaShape==ArenaShape.Rectangle) {
				if(ImGui.Button("Width = my dx")) {
					configuration.ArenaSize=Math.Clamp(MathF.Abs(num2),1f,200f);
					configuration.Save();
				}
				SettingTip("Stand on the left or right edge and press this.");
				ImGui.SameLine();
				if(ImGui.Button("Depth = my dz")) {
					configuration.ArenaSizeZ=Math.Clamp(MathF.Abs(num3),1f,200f);
					configuration.Save();
				}
				SettingTip("Stand on the near or far edge and press this.");
			}
			else if(ImGui.Button("Set size to my distance")) {
				configuration.ArenaSize=Math.Clamp(value,1f,200f);
				configuration.Save();
			}
			if(configuration.ArenaShape!=ArenaShape.Rectangle) {
				SettingTip("Stand on the edge of the board and press this - the boundary snaps to where you are, which is quicker than hunting for it on the slider.");
			}
		}
	}
	private void DrawArenaAppearance() {
		Vector4 col=new Vector4(configuration.ArenaColourR,configuration.ArenaColourG,configuration.ArenaColourB,configuration.ArenaColourA);
		ImGui.SetNextItemWidth(240f);
		if(ImGui.ColorEdit4("Colour",ref col)) {
			configuration.ArenaColourR=col.X;
			configuration.ArenaColourG=col.Y;
			configuration.ArenaColourB=col.Z;
			configuration.ArenaColourA=col.W;
			configuration.Save();
		}
		float v=configuration.ArenaThickness;
		ImGui.SetNextItemWidth(240f);
		if(ImGui.SliderFloat("Thickness",ref v,0.5f,10f,"%.1f")) {
			configuration.ArenaThickness=v;
			configuration.Save();
		}
		if(ImGui.Checkbox("Mark the centre",ref configuration.ArenaShowCentre)) {
			configuration.Save();
		}
	}
	private void DrawArenaReadout() {
		ushort num=((configuration.ArenaCentreMode==ArenaCentreMode.Fixed&&configuration.ArenaCentreTerritory!=0)?configuration.ArenaCentreTerritory:((ushort)DalamudApi.ClientState.TerritoryType));
		string value=BeastmasterGameData.ZoneName(num,$"territory {num}");
		Vector3 vector=((configuration.ArenaCentreMode==ArenaCentreMode.Fixed)?new Vector3(configuration.ArenaCentreX,configuration.ArenaCentreY,configuration.ArenaCentreZ):(DalamudApi.ObjectTable.LocalPlayer?.Position ?? Vector3.Zero));
		string text=((configuration.ArenaShape!=ArenaShape.Circle&&configuration.ArenaRotationDegrees>0.5f)?$" rot={configuration.ArenaRotationDegrees:0}deg":string.Empty);
		string value2=configuration.ArenaShape switch {
			ArenaShape.Circle => $"circle r={configuration.ArenaSize:0.#}y (diameter {configuration.ArenaSize*2f:0.#}y)",
			ArenaShape.Rectangle => $"rectangle half=({configuration.ArenaSize:0.#} x {configuration.ArenaSizeZ:0.#})y (full {configuration.ArenaSize*2f:0.#} x {configuration.ArenaSizeZ*2f:0.#}y)"+text,
			_ => $"square half={configuration.ArenaSize:0.#}y (full {configuration.ArenaSize*2f:0.#}y)"+text,
		};
		string text2=$"territory={num} ({value}) | {value2} | centre=({vector.X:0.###}, {vector.Y:0.###}, {vector.Z:0.###})";
		ImGui.TextWrapped(text2);
		if(ImGui.Button("Copy measurement")) {
			ImGui.SetClipboardText(text2);
		}
		SettingTip("Copies the line above, ready to paste back so the board can be recorded.");
	}
	private void DrawCrucibleBoardOptions() {
		ImGui.Text("Crucible Board(s) Options");
		ImGui.Separator();
		ImGui.TextWrapped("What the Icy Veins Crucible of the Unbroken guide suggests for each board: the familiars to enter with, the three to fight each enemy with, the enemy's abilities and how the fight plays out. Only Suggested Team Mode uses this - Recommended Team Mode, Level Up Familiar(s) Mode and the rotation are unchanged. In a fight it has a plan for, Suggested Team Mode also decides how each familiar is used: it Borrows from the familiars the strategy needs (interrupts, cleanses, dispels, mitigation before a tank buster) and Tempered Releases the rest, whatever the One with Nature setting says. The strategy text itself is shown for reference.");
		if(ImGui.Button("Open the guide##BmhGuideOpen")) {
			Process.Start(new ProcessStartInfo("https://www.icy-veins.com/ffxiv/beastmaster-crucible-of-the-unbroken") {
				UseShellExecute = true
			});
		}
		ImGui.SameLine();
		DrawResetBoardOptionsButton();
		ImGui.Spacing();
		if(!ImGui.BeginTabBar("##BmhBoardTabs")) {
			return;
		}
		for(int i=0;i<BeastmasterCrucibleService.BoardNames.Length;i++) {
			ImU8String label=new ImU8String(10,2);
			label.AppendFormatted(BeastmasterCrucibleService.BoardNames[i]);
			label.AppendLiteral("##boardtab");
			label.AppendFormatted(i);
			if(ImGui.BeginTabItem(label)) {
				DrawBoardOptionsTab(i);
				ImGui.EndTabItem();
			}
		}
		ImGui.EndTabBar();
	}
	private void DrawResetBoardOptionsButton() {
		if(!resetBoardOptionsConfirming) {
			if(ImGui.Button("Reset to dev-planned defaults##BmhResetBoardOptions")) {
				resetBoardOptionsConfirming=true;
			}
			if(ImGui.IsItemHovered()) {
				ImGui.SetTooltip("Drops every board and fight back to what the guide itself recommends: each fight's own three familiars, and each familiar's Borrow-or-Tempered-Release pick. Anything you have not changed by hand is already the guide's recommendation and is not affected.");
			}
			return;
		}
		ImGui.TextColored(new Vector4(1f,0.8f,0.4f,1f),"Discard every hand-made team and Borrow/Tempered choice, on every board?");
		if(ImGui.Button("Yes, reset them##BmhResetBoardOptionsConfirm")) {
			configuration.ResetCrucibleBoardOptionsToGuide();
			resetBoardOptionsConfirming=false;
			DalamudApi.ChatGui.Print("[Beast Master Helper] Crucible Board(s) Options reset to the guide's own recommendations.");
		}
		ImGui.SameLine();
		if(ImGui.Button("Cancel##BmhResetBoardOptionsCancel")) {
			resetBoardOptionsConfirming=false;
		}
	}
	private void DrawBoardOptionsTab(int board) {
		CrucibleGuideBoard crucibleGuideBoard=CrucibleBoardGuide.ForBoard(board);
		if(crucibleGuideBoard==null) {
			ImGui.TextWrapped("Not built yet. This board's guide data has not been added.");
			return;
		}
		bool v=configuration.CrucibleGuideUseEntryPool;
		if(ImGui.Checkbox("Register the guide's entry team on entry##BmhGuideEntry",ref v)) {
			configuration.CrucibleGuideUseEntryPool=v;
			configuration.Save();
		}
		bool v2=configuration.CrucibleGuideUseFightTeams;
		if(ImGui.Checkbox("Pick each fight's team from the guide##BmhGuideFights",ref v2)) {
			configuration.CrucibleGuideUseFightTeams=v2;
			configuration.Save();
		}
		ImGui.TextUnformatted("Snarl / Challenge in guide fights: managed automatically");
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Not a toggle - this runs whenever Suggested Team Mode has a fight plan active, full stop. Whichever familiar is out covers you with Snarl while it is healthy, and hands off once it has taken its share so the team ends up sharing the fight's damage rather than one familiar absorbing it alone. During a tank buster you take it yourself (Challenge) with the mitigation you have lent. In an emergency, or while you are hurting and the familiar still has something to give, it covers you regardless. Replaces Keep Covered in these fights.");
		}
		bool flag=CrucibleEnmityPlan.Applies(configuration);
		Vector4 col;
		if(!configuration.CrucibleSuggestedTeamMode) {
			ImGui.TextColored(new Vector4(1f,0.6f,0.4f,1f),"Not active: Suggested Team Mode is off (overlay's Crucible tab) - Keep Covered decides Snarl/Challenge instead.");
		}
		else if(CrucibleFightPlan.Active==null) {
			ImGui.TextColored(new Vector4(1f,0.6f,0.4f,1f),"Not active: Suggested Team Mode is on, but no guide fight is currently recognised - Keep Covered decides Snarl/Challenge instead. This clears the moment a fight the guide knows starts.");
		}
		else if(flag) {
			col=new Vector4(0.5f,1f,0.5f,1f);
			ImU8String text=new ImU8String(20,1);
			text.AppendLiteral("Active - plan is \"");
			text.AppendFormatted(CrucibleFightPlan.Active.Name);
			text.AppendLiteral("\".");
			ImGui.TextColored(in col,text);
		}
		else {
			col=new Vector4(1f,0.6f,0.4f,1f);
			ImU8String text2=new ImU8String(97,1);
			text2.AppendLiteral("Not active: plan is \"");
			text2.AppendFormatted(CrucibleFightPlan.Active.Name);
			text2.AppendLiteral("\" but you have left the duty - Keep Covered decides Snarl/Challenge instead.");
			ImGui.TextColored(in col,text2);
		}
		ImGui.Indent();
		int v3=configuration.CrucibleEnmityEmergencyPercent;
		ImGui.SetNextItemWidth(120f);
		if(ImGui.SliderInt("Emergency: you at or below (%)##BmhEnmityEmergency",ref v3,1,40)) {
			configuration.CrucibleEnmityEmergencyPercent=v3;
			configuration.Save();
		}
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("At or below this the familiar covers you whatever its own health, until you are back above 50%.");
		}
		int v4=configuration.CrucibleEnmityHurtPercent;
		ImGui.SetNextItemWidth(120f);
		if(ImGui.SliderInt("Hurt: you at or below (%)##BmhEnmityHurt",ref v4,1,60)) {
			configuration.CrucibleEnmityHurtPercent=v4;
			configuration.Save();
		}
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("At or below this, the familiar keeps covering you even if it has already taken its share and would otherwise hand enmity back - unless it has nothing left to give either. Held until you are back above 60%. Lower severity than Emergency above: this only overrides the share floor, not a familiar with no health left.");
		}
		int v5=configuration.CrucibleEnmityShareFloor;
		ImGui.SetNextItemWidth(120f);
		if(ImGui.SliderInt("Familiar hands off enmity at (%)##BmhEnmityShare",ref v5,5,95)) {
			configuration.CrucibleEnmityShareFloor=v5;
			configuration.Save();
		}
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Set high on purpose - the familiar out hands enmity back to you once its own HP reaches this, so it takes a share of the damage rather than being fought down to the floor alone.");
		}
		if(CrucibleEnmityPlan.LastWhy.Length>0) {
			ImGui.TextDisabled("Last decision: "+CrucibleEnmityPlan.LastWhy);
		}
		ImGui.Unindent();
		ImGui.TextDisabled(configuration.CrucibleSuggestedTeamMode?"Suggested Team Mode is ON.":"Suggested Team Mode is off - turn it on in the overlay's Crucible tab to use this.");
		ImGui.Spacing();
		col=new Vector4(1f,0.85f,0.3f,1f);
		ImU8String text3=new ImU8String(23,1);
		text3.AppendLiteral("Entry team (");
		text3.AppendFormatted(crucibleGuideBoard.EntryPool.Length);
		text3.AppendLiteral(" familiars)");
		ImGui.TextColored(in col,text3);
		ImGui.Separator();
		int[] entryPool=crucibleGuideBoard.EntryPool;
		foreach(int number in entryPool) {
			DrawGuideFamiliar(number);
			ImGui.SameLine();
		}
		ImGui.NewLine();
		ImGui.Spacing();
		ImGui.TextColored(new Vector4(1f,0.85f,0.3f,1f),"Fights");
		ImGui.Separator();
		ImU8String label=new ImU8String(56,1);
		label.AppendLiteral("Let Reaction Helper run every fight on this board##rhall");
		label.AppendFormatted(board);
		if(ImGui.Button(label)) {
			foreach(CrucibleGuideFight fight in crucibleGuideBoard.Fights) {
				configuration.CrucibleReactionHelperFights.Add(CrucibleBoardGuide.TeamKey(board,fight));
			}
			configuration.Save();
		}
		ImGui.SameLine();
		ImU8String label2=new ImU8String(46,1);
		label2.AppendLiteral("Let BMH plan every fight on this board##bmhall");
		label2.AppendFormatted(board);
		if(ImGui.Button(label2)) {
			foreach(CrucibleGuideFight fight2 in crucibleGuideBoard.Fights) {
				configuration.CrucibleReactionHelperFights.Remove(CrucibleBoardGuide.TeamKey(board,fight2));
			}
			configuration.Save();
		}
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Unticks 'Let Reaction Helper run this fight' below for every fight on this board, so BMH's own planning (CrucibleEnmityPlan/CrucibleFightPlan) handles all of them again.");
		}
		int num=0;
		foreach(CrucibleGuideFight fight3 in crucibleGuideBoard.Fights) {
			num++;
			ImU8String label3=new ImU8String(18,5);
			label3.AppendFormatted(num);
			label3.AppendLiteral(". ");
			label3.AppendFormatted(fight3.Name);
			label3.AppendLiteral(" (");
			label3.AppendFormatted(fight3.Kind);
			label3.AppendLiteral(")##guidefight");
			label3.AppendFormatted(board);
			label3.AppendLiteral("_");
			label3.AppendFormatted(num);
			if(ImGui.CollapsingHeader(label3)) {
				DrawGuideFight(board,fight3);
			}
		}
	}
	private void DrawGuideFamiliar(int number) {
		ImGui.Image(DalamudApi.Textures.GetFromGameIcon(new GameIconLookup((uint)(242000+number))).GetWrapOrEmpty().Handle,new Vector2(32f,32f));
		if(ImGui.IsItemHovered()) {
			BeastKin beastKin=BeastmasterKinship.KinOf(number);
			int? num=CrucibleBeastRank.EffectiveRank(configuration.CrucibleBeastRankLearned,configuration.CrucibleBeastRankFallback,number);
			ImGui.SetTooltip($"{FamiliarLabel(number)}\nKinship: {((beastKin==BeastKin.Unknown)?"-":beastKin.ToString())}\nCrucible rank: {num?.ToString() ?? "?"}\nUtility: {FamiliarUtility.Describe(number)}\n"+string.Join("\n",FamiliarUtility.Details(number)));
		}
	}
	private static string FamiliarLabel(int number) {
		return BeastmasterCatalog.Entries.FirstOrDefault((BeastmasterCatalogEntry e) => e.Number==number)?.DisplayName ?? $"No. {number}";
	}
	private void DrawFamiliarUse(int board,CrucibleGuideFight fight,int number) {
		(PlannedUse Use,string Why) tuple=CrucibleFightPlan.RecommendedUse(fight,number);
		PlannedUse item=tuple.Use;
		string item2=tuple.Why;
		bool flag=false;
		ImGui.TextUnformatted("Suggested for this fight: "+Label(item));
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("A suggestion only. Reaction Helper (or the Borrow / Tempered Release switches) decides what is actually used.");
		}
		FamiliarInfo familiarInfo=FamiliarAbilityData.Get(number);
		string text;
		if(item==PlannedUse.Borrow) {
			BeastKin beastKin=BeastmasterKinship.KinOf(number);
			text=((beastKin==BeastKin.Unknown)?"Borrow: its kinship action.":BeastmasterKinship.Effect(beastKin));
		}
		else {
			text=((familiarInfo==null)?"Tempered Release.":(familiarInfo.Tempered.Name+": "+familiarInfo.Tempered.Effect+((familiarInfo.Tempered.Target.Length>0)?(" ("+familiarInfo.Tempered.Target+")"):string.Empty)+(familiarInfo.TemperedLeaves?" [LEAVES THE FIGHT]":string.Empty)));
		}
		bool flag2=item==PlannedUse.TemperedRelease&&(familiarInfo?.TemperedLeaves ?? false);
		Vector4 col;
		if(flag2) {
			col=new Vector4(1f,0.5f,0.4f,1f);
		}
		else {
			col=((item==PlannedUse.Borrow)?new Vector4(0.55f,0.9f,0.55f,1f):new Vector4(1f,0.85f,0.45f,1f));
		}
		ImGui.PushStyleColor(ImGuiCol.Text,col);
		ImGui.TextWrapped(text);
		ImGui.PopStyleColor();
		if(!flag) {
			ImGui.TextDisabled("Recommended: "+item2);
		}
		static string Label(PlannedUse u) {
			if(u!=PlannedUse.Borrow) {
				return "Tempered Release";
			}
			return "Borrow";
		}
	}
	private void DrawGuideFight(int board,CrucibleGuideFight fight) {
		string text=CrucibleBoardGuide.TeamKey(board,fight);
		bool v=configuration.CrucibleReactionHelperFights.Contains(text);
		ImU8String label=new ImU8String(61,1);
		label.AppendLiteral("Let Reaction Helper run this fight (BMH does not plan it)##rh");
		label.AppendFormatted(text);
		if(ImGui.Checkbox(label,ref v)) {
			if(v) {
				configuration.CrucibleReactionHelperFights.Add(text);
			}
			else {
				configuration.CrucibleReactionHelperFights.Remove(text);
			}
			configuration.Save();
		}
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Turns off the plan below for this fight - which familiar Borrows or is released, summon order, holds - so a Reaction Helper\nprofile can do it instead. The rotation itself keeps running.");
		}
		bool flag=configuration.CrucibleFightTeamOverrides.TryGetValue(text,out int[] value)&&value.Length==3;
		int[] array=(flag?value:fight.Team);
		ImGui.TextUnformatted(flag?"Team (your own choice):":"Team (the guide's):");
		List<BeastmasterCatalogEntry> list=BeastmasterCatalog.Entries.OrderBy((BeastmasterCatalogEntry e) => e.Number).ToList();
		ImU8String strId=new ImU8String(11,1);
		strId.AppendLiteral("##teamtable");
		strId.AppendFormatted(text);
		if(ImGui.BeginTable(strId,3,ImGuiTableFlags.SizingStretchProp|ImGuiTableFlags.RowBg)) {
			ImGui.TableSetupColumn("##icon",ImGuiTableColumnFlags.WidthFixed,38f);
			ImGui.TableSetupColumn("Familiar",ImGuiTableColumnFlags.WidthFixed,190f);
			ImGui.TableSetupColumn("Utility",ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableHeadersRow();
			for(int num=0;num<3;num++) {
				ImGui.TableNextRow();
				ImGui.TableNextColumn();
				DrawGuideFamiliar(array[num]);
				ImGui.TableNextColumn();
				ImGui.SetNextItemWidth(-1f);
				ImU8String label2=new ImU8String(6,2);
				label2.AppendLiteral("##team");
				label2.AppendFormatted(text);
				label2.AppendFormatted(num);
				if(ImGui.BeginCombo(label2,FamiliarLabel(array[num]))) {
					foreach(BeastmasterCatalogEntry item in list) {
						ImU8String label3=new ImU8String(6,4);
						label3.AppendFormatted(item.DisplayName);
						label3.AppendLiteral("##pick");
						label3.AppendFormatted(text);
						label3.AppendFormatted(num);
						label3.AppendFormatted(item.Number);
						if(ImGui.Selectable(label3,item.Number==array[num])) {
							int[] array2=(int[])array.Clone();
							array2[num]=item.Number;
							configuration.CrucibleFightTeamOverrides[text]=array2;
							configuration.Save();
						}
					}
					ImGui.EndCombo();
				}
				ImGui.TableNextColumn();
				ImGui.TextColored(new Vector4(0.6f,0.85f,1f,1f),FamiliarUtility.Describe(array[num]));
				DrawFamiliarUse(board,fight,array[num]);
				foreach(string item2 in FamiliarUtility.Details(array[num])) {
					bool flag2=item2.Contains("LEAVES THE FIGHT",StringComparison.Ordinal);
					ImGui.PushStyleColor(ImGuiCol.Text,flag2?new Vector4(1f,0.5f,0.4f,1f):ImGui.GetStyle().Colors[1]);
					ImGui.TextWrapped(item2);
					ImGui.PopStyleColor();
				}
			}
			ImGui.EndTable();
		}
		if(flag) {
			ImGui.SameLine();
			ImU8String label4=new ImU8String(21,1);
			label4.AppendLiteral("Reset to guide##reset");
			label4.AppendFormatted(text);
			if(ImGui.SmallButton(label4)) {
				configuration.CrucibleFightTeamOverrides.Remove(text);
				configuration.Save();
			}
		}
		string[] wants=fight.Wants;
		foreach(string want in wants) {
			List<string> list2=array.Where((int number) => FamiliarUtility.Covers(number,want)).Select(FamiliarLabel).ToList();
			if(list2.Count>0) {
				Vector4 col=new Vector4(0.45f,0.9f,0.5f,1f);
				ImU8String text2=new ImU8String(19,2);
				text2.AppendLiteral("Needs ");
				text2.AppendFormatted(want);
				text2.AppendLiteral(": covered by ");
				text2.AppendFormatted(string.Join(", ",list2));
				ImGui.TextColored(in col,text2);
			}
			else {
				Vector4 col=new Vector4(1f,0.65f,0.3f,1f);
				ImU8String text3=new ImU8String(52,1);
				text3.AppendLiteral("Needs ");
				text3.AppendFormatted(want);
				text3.AppendLiteral(": no familiar in this team is known to have it");
				ImGui.TextColored(in col,text3);
			}
		}
		if(fight.Busters.Length!=0) {
			Vector4 col=new Vector4(1f,0.6f,0.4f,1f);
			ImU8String text4=new ImU8String(60,1);
			text4.AppendLiteral("Tank buster(s): ");
			text4.AppendFormatted(string.Join(", ",fight.Busters));
			text4.AppendLiteral(" - mitigation is pressed as the cast starts.");
			ImGui.TextColored(in col,text4);
		}
		if(fight.Interrupts.Length!=0) {
			Vector4 col=new Vector4(0.6f,0.85f,1f,1f);
			ImU8String text5=new ImU8String(25,1);
			text5.AppendLiteral("Soul Crush is kept for: ");
			text5.AppendFormatted(string.Join(", ",fight.Interrupts));
			text5.AppendLiteral(".");
			ImGui.TextColored(in col,text5);
		}
		if(fight.Dispels.Length!=0) {
			Vector4 col=new Vector4(0.6f,0.85f,1f,1f);
			ImU8String text6=new ImU8String(24,1);
			text6.AppendLiteral("Quelling Wave dispels: ");
			text6.AppendFormatted(string.Join(", ",fight.Dispels));
			text6.AppendLiteral(".");
			ImGui.TextColored(in col,text6);
		}
		if(fight.Why.Length>0) {
			ImGui.TextDisabled("Why this team: "+fight.Why);
		}
		if(fight.Abilities.Length!=0) {
			ImU8String label5=new ImU8String(24,2);
			label5.AppendLiteral("Enemy abilities (");
			label5.AppendFormatted(fight.Abilities.Length);
			label5.AppendLiteral(")##abil");
			label5.AppendFormatted(text);
			if(ImGui.TreeNode(label5)) {
				wants=fight.Abilities;
				foreach(string obj in wants) {
					ImGui.Bullet();
					ImGui.SameLine();
					ImGui.TextWrapped(obj);
				}
				ImGui.TreePop();
			}
		}
		if(fight.Notes.Length!=0) {
			ImU8String id=new ImU8String(15,1);
			id.AppendLiteral("Strategy##strat");
			id.AppendFormatted(text);
			if(ImGui.TreeNodeEx(id,ImGuiTreeNodeFlags.DefaultOpen)) {
				wants=fight.Notes;
				foreach(string obj2 in wants) {
					ImGui.Bullet();
					ImGui.SameLine();
					ImGui.TextWrapped(obj2);
				}
				ImGui.TreePop();
			}
		}
		ImGui.Spacing();
	}
	private static string SaveBoardDump(string dump) {
		try {
			string pluginConfigDirectory=DalamudApi.PluginInterface.GetPluginConfigDirectory();
			Directory.CreateDirectory(pluginConfigDirectory);
			string text=Path.Combine(pluginConfigDirectory,$"crucible-board-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
			File.WriteAllText(text,dump);
			DalamudApi.ChatGui.Print("[Beast Master Helper] Board window dump saved to "+text);
			return text;
		}
		catch(Exception ex) {
			DalamudApi.Log.Warning(ex,"Could not save the board window dump.");
			return "could not save: "+ex.Message;
		}
	}
	private void DrawCrucibleBoardAssist() {
		ImGui.TextUnformatted("On the board");
		bool v=configuration.CrucibleBoardAssist;
		if(ImGui.Checkbox("Handle board windows",ref v)) {
			configuration.CrucibleBoardAssist=v;
			configuration.Save();
		}
		SettingTip("Deals with the windows that appear while traversing a board: loot after a fight, the treasure choice, and spending a healing item when it is worth spending.");
		if(configuration.CrucibleBoardAssist) {
			ImGui.Indent();
			bool v2=configuration.CrucibleAutoAdvance;
			if(ImGui.Checkbox("Walk to the next platform",ref v2)) {
				configuration.CrucibleAutoAdvance=v2;
				configuration.Save();
			}
			SettingTip("Needs vnavmesh, and a surveyed map for the board. All five boards (three Unbroken, two Master's) are mapped - on any board that is not, this does nothing rather than walking to a coordinate nobody measured.");
			if(configuration.CrucibleAutoAdvance) {
				bool v3=configuration.CrucibleCampsiteWhenHurt;
				if(ImGui.Checkbox("Prefer the campsite branch when hurt",ref v3)) {
					configuration.CrucibleCampsiteWhenHurt=v3;
					configuration.Save();
				}
				SettingTip("The board branches, one side to a treasure and the other to a campsite. The treasure is the default: a campsite is spent instead of a chest, and a chest not opened is gone for the run. The campsite side is only taken when you, or your fighting team as a whole, are below the thresholds below.");
				if(configuration.CrucibleCampsiteWhenHurt) {
					bool v4=configuration.CrucibleCheckTeamBeforeFork;
					if(ImGui.Checkbox("Check the team's HP before a campsite fork",ref v4)) {
						configuration.CrucibleCheckTeamBeforeFork=v4;
						configuration.Save();
					}
					SettingTip("Before leaving a platform that leads to a fork with a campsite, opens Board Layout from the board HUD to read every familiar's current HP, then closes it. Without this the choice uses the HP from the last time the team window happened to be open.");
					int v5=configuration.CrucibleCampsiteTotalMissing;
					ImGui.SetNextItemWidth(200f);
					if(ImGui.SliderInt("Campsite when a rest gives back",ref v5,30,300,"%d points of HP")) {
						configuration.CrucibleCampsiteTotalMissing=v5;
						configuration.Save();
					}
					SettingTip("Adds up the HP a campsite could restore: yours plus the three most hurt familiars' (each measured as percent missing). At or above this the campsite is taken even when you are healthy, so a worn-down team is not passed by. 100 is one whole health bar.");
					float v6=configuration.CrucibleCampsiteHpPercent;
					ImGui.SetNextItemWidth(200f);
					if(ImGui.SliderFloat("Campsite below",ref v6,1f,100f,"%.0f%% HP")) {
						configuration.CrucibleCampsiteHpPercent=Math.Clamp(v6,1f,100f);
						configuration.Save();
					}
					SettingTip("Your own HP.");
					float v7=configuration.CrucibleCampsiteFamiliarHpPercent;
					ImGui.SetNextItemWidth(200f);
					if(ImGui.SliderFloat("Familiar counts as hurt below",ref v7,1f,100f,"%.0f%% HP")) {
						configuration.CrucibleCampsiteFamiliarHpPercent=Math.Clamp(v7,1f,100f);
						configuration.Save();
					}
					int v8=configuration.CrucibleCampsiteHurtFamiliars;
					ImGui.SetNextItemWidth(200f);
					if(ImGui.SliderInt("Campsite when this many are hurt",ref v8,1,3)) {
						configuration.CrucibleCampsiteHurtFamiliars=Math.Clamp(v8,1,3);
						configuration.Save();
					}
					SettingTip("Counted across the three familiars you fight with; a dead one counts as hurt. Two is the default because one scratched familiar can be benched or given an Ash, while two or three hurt is a team that needs the rest. The familiar out is read live; the other two come from the team window as it last showed them, which is before the last fight.");
				}
			}
			bool v9=configuration.CrucibleTakeAllLoot;
			if(ImGui.Checkbox("Choose loot individually by priority",ref v9)) {
				configuration.CrucibleTakeAllLoot=v9;
				configuration.Save();
			}
			SettingTip("Takes Territory Tokens and wanted item cards individually. Temporal Sand is never taken; the remaining booty window is closed to discard it.");
			bool v10=configuration.CrucibleAutoTreasure;
			if(ImGui.Checkbox("Choose treasure automatically",ref v10)) {
				configuration.CrucibleAutoTreasure=v10;
				configuration.Save();
			}
			SettingTip("The shop charges tokens and holding space is limited, so it works down a preference order and lets the game refuse what cannot be afforded. Beast Potions and Crucible Ashes are bought freely up to the stock below, since they are cheap and one will be wanted in a later fight. Everything else is ranked by what its effect text says it does rather than by price - price tracks rarity, and the rarest thing on the shelf is not always the most useful.");
			int v11=configuration.CrucibleHealingStock;
			ImGui.SetNextItemWidth(200f);
			if(ImGui.SliderInt("Keep potions in stock",ref v11,0,10)) {
				configuration.CrucibleHealingStock=Math.Clamp(v11,0,10);
				configuration.Save();
			}
			SettingTip("How many Beast Potions and Crucible Ashes to stock up to before gear gets a look in. Zero buys none.");
			bool v12=configuration.CrucibleAutoHeal;
			if(ImGui.Checkbox("Use healing items",ref v12)) {
				configuration.CrucibleAutoHeal=v12;
				configuration.Save();
			}
			SettingTip("Sized by what you are missing, with no threshold to set. The Beast Potions restore 10, 23, 36 and 50 percent, so missing 10% takes a G1, missing 36% a G3, and the largest one that is fully absorbed is always the one used. Missing less than the smallest potion restores means none is spent.");
			float v13=configuration.CrucibleDesperateHpPercent;
			ImGui.SetNextItemWidth(200f);
			if(ImGui.SliderFloat("Stock extra potions below",ref v13,1f,100f,"%.0f%% HP")) {
				configuration.CrucibleDesperateHpPercent=Math.Clamp(v13,1f,100f);
				configuration.Save();
			}
			ImGui.Unindent();
		}
		ImGui.TextDisabled(crucibleBoardService.Status);
		ImGui.TextWrapped(crucibleBoardService.ShopPlan);
		ImGui.TextWrapped(crucibleBoardService.TreasurePlan);
		ImGui.TextWrapped(crucibleBoardService.LootPlan);
		ImGui.TextWrapped(crucibleBoardService.BuffStatus);
		ImGui.TextWrapped(crucibleBoardService.BranchPlan);
		ImGui.TextWrapped(crucibleBoardService.CapacityPlan);
		ImGui.TextWrapped("Crucible Feather: "+crucibleBoardService.FeatherStatus);
		bool v14=configuration.CrucibleLevelUpBeasts;
		if(ImGui.Checkbox("Level Up Beasts",ref v14)) {
			SelectCrucibleMode(v14?CrucibleMode.LevelUp:CrucibleMode.None);
		}
		SettingTip("Each run: clear the loaded team, open the Master's Bestiary from Team Composition, read every familiar's Beast Rank one at a time on both pages, then fill the team with the lowest-ranked familiars before challenging. Replaces Recommended Team while it is on.\n\nNew and not yet confirmed in game.");
		int data=configuration.CrucibleGridBufferMs;
		ImGui.SetNextItemWidth(100f);
		if(ImGui.InputInt("Bestiary click buffer (ms)",ref data)) {
			configuration.CrucibleGridBufferMs=Math.Clamp(data,5,500);
			configuration.Save();
		}
		SettingTip("How long to wait after the Bestiary grid confirms an unselect, a select or a page switch before the next one, in Level Up and Suggested Team modes. Lower is faster (5 is the minimum); raise it if a click is ever missed. Default 50.");
		if(configuration.CrucibleLevelUpBeasts) {
			int num=CrucibleBeastRank.BoardRankCap(Math.Clamp(configuration.CrucibleBoard,0,BeastmasterCrucibleService.BoardNames.Length-1));
			ImGui.TextWrapped($"Fields the three lowest-ranked familiars that can still gain here: below the board's rank cap ({num}), or at it with the exp bar not yet full. "+"Caps: 5, 10, 15, 20 and 25 for the boards in order.");
			int data2=configuration.CrucibleLevelRescanEvery;
			ImGui.SetNextItemWidth(100f);
			if(ImGui.InputInt("Re-read all familiars every N runs",ref data2)) {
				configuration.CrucibleLevelRescanEvery=Math.Clamp(data2,1,100);
				configuration.Save();
			}
			SettingTip("Reading all fifty familiars' rank and exp takes a while, and the pick rarely changes from one run to the next. The runs in between keep the same team and skip the Bestiary entirely. 1 reads every run.");
			ImGui.TextWrapped(CrucibleBestiaryReader.Describe(configuration.CrucibleBeastRankLearned,configuration.CrucibleBeastRankFallback,num-1));
			DrawCrucibleBeastRankFallback();
		}
		int v15=configuration.CruciblePlayerBuyLimit;
		ImGui.SetNextItemWidth(200f);
		if(ImGui.SliderInt("Player beast gear per shop",ref v15,1,2)) {
			configuration.CruciblePlayerBuyLimit=v15;
			configuration.Save();
		}
		SettingTip("One or two pieces of Beast Gear are selected after G2+ healing and the named priority consumables, while preserving the planned familiar buffs.");
		int v16=configuration.CrucibleFamiliarBuffTarget;
		ImGui.SetNextItemWidth(200f);
		if(ImGui.SliderInt("Familiar buffs per shop",ref v16,2,6)) {
			configuration.CrucibleFamiliarBuffTarget=v16;
			configuration.Save();
		}
		SettingTip("The full shelf and exact token balance are used to reserve distinct familiar buff rows. The final purchase set cannot buy beyond this target.");
		bool v17=configuration.CrucibleUseTomeInFight;
		if(ImGui.Checkbox("Use Tome of the Impervious in fights",ref v17)) {
			configuration.CrucibleUseTomeInFight=v17;
			configuration.Save();
		}
		SettingTip("Covers every fight consumable that grants a status: the Blink and Reflect tomes, Vampiric Essence, the Beast Potion Kit and Merchant's Eye. Each is spent once per fight and confirmed by the status actually appearing. The tomes follow Keep Covered - familiar when it is on, player when it is off.");
		float v18=configuration.CrucibleAutoPotionHpPercent;
		ImGui.SetNextItemWidth(200f);
		if(ImGui.SliderFloat("Pop the Potion Kit at",ref v18,10f,90f,"%.0f%% HP")) {
			configuration.CrucibleAutoPotionHpPercent=Math.Clamp(v18,1f,100f);
			configuration.Save();
		}
		SettingTip("The Potion Kit restores 40% when your HP is below half. It is used on you (never the familiar) once HP drops under this, ahead of any Beast Potion, so a regular potion is not spent on a hit the kit covers. 50% is the kit's own trigger.");
		int v19=configuration.CruciblePlayerPotionReserve;
		ImGui.SetNextItemWidth(200f);
		if(ImGui.SliderInt("Keep Beast Potions for yourself",ref v19,0,10)) {
			configuration.CruciblePlayerPotionReserve=v19;
			configuration.Save();
		}
		SettingTip("A Beast Potion reads \"self or familiar\", so anything held beyond this reserve can be given to a hurt familiar in a fight. The familiar cannot drink the Ashes and cannot be revived, so the reserve is kept before any are handed over.");
		float v20=configuration.CrucibleFeedMinHpPercent;
		ImGui.SetNextItemWidth(200f);
		if(ImGui.SliderFloat("Only feed above",ref v20,0f,100f,"%.0f%% HP")) {
			configuration.CrucibleFeedMinHpPercent=Math.Clamp(v20,0f,100f);
			configuration.Save();
		}
		SettingTip("A feed is permanent growth, not a heal. A familiar already chewed down has had its turn and is not going into the next fight, so growth spent on it is never used. Preferring the healthiest is not enough on its own: once the kinship, buff-slot and variety rules have excluded everyone healthy, the best of what is left is still a familiar that should not be fed.");
		IReadOnlyList<CrucibleItem> held=crucibleBoardService.Held;
		if(held.Count>0) {
			ImU8String text=new ImU8String(28,1);
			text.AppendLiteral("Holding ");
			text.AppendFormatted(held.Count);
			text.AppendLiteral(" recognised item(s):");
			ImGui.TextDisabled(text);
			foreach(CrucibleItem item in held) {
				string value=(item.Heals?$"restores {item.HealPercent}%":item.Summary);
				ImU8String text2=new ImU8String(6,2);
				text2.AppendLiteral("   ");
				text2.AppendFormatted(item.Name);
				text2.AppendLiteral(" - ");
				text2.AppendFormatted(value);
				ImGui.TextDisabled(text2);
			}
		}
		DrawCrucibleClickRecorder();
		if(!ImGui.CollapsingHeader("Board window contents##BmhBoardDump")) {
			return;
		}
		if(ImGui.Button("Capture board windows")) {
			crucibleBoardDump=CrucibleContents.DescribeFull();
		}
		ImGui.SameLine();
		if(ImGui.Button("Copy##BmhBoardDumpCopy")) {
			ImGui.SetClipboardText(crucibleBoardDump);
		}
		ImGui.SameLine();
		if(ImGui.Button("Save to file##BmhBoardDumpSave")) {
			crucibleBoardDumpPath=SaveBoardDump(crucibleBoardDump);
		}
		if(!string.IsNullOrEmpty(crucibleBoardDumpPath)) {
			ImGui.SameLine();
			if(ImGui.Button("Copy path##BmhBoardDumpCopyPath")) {
				ImGui.SetClipboardText(crucibleBoardDumpPath);
			}
			ImU8String text3=new ImU8String(9,1);
			text3.AppendLiteral("Saved to ");
			text3.AppendFormatted(crucibleBoardDumpPath);
			ImGui.TextWrapped(text3);
		}
		ImU8String text4=new ImU8String(20,1);
		text4.AppendFormatted(crucibleBoardDump.Length);
		text4.AppendLiteral(" characters captured");
		ImGui.TextDisabled(text4);
		ImGui.TextWrapped((crucibleBoardDump.Length>4000)?(crucibleBoardDump.Substring(0,4000)+Environment.NewLine+"... (truncated, use Save to file)"):crucibleBoardDump);
	}
	private void DrawCrucible() {
		ImGui.TextUnformatted("Crucible of the Unbroken");
		ImGui.TextWrapped("Travel to Lauda, select a board, prepare a team and commence the duty.");
		ImGui.TextDisabled("Entry automation only. BossmodReborn handles supported encounter mechanics.");
		ImGui.Separator();
		ImGui.BeginDisabled(crucibleService.IsRunning);
		int currentItem=Math.Clamp(configuration.CrucibleBoard,0,BeastmasterCrucibleService.BoardNames.Length-1);
		if(ImGui.Combo("Board",ref currentItem,"First Board\0Second Board\0Third Board\0First Master's Board\0Second Master's Board\0")) {
			configuration.CrucibleBoard=currentItem;
			configuration.Save();
		}
		bool v=configuration.CrucibleRecommendedTeam;
		if(ImGui.Checkbox("Recommended Team",ref v)) {
			SelectCrucibleMode(v?CrucibleMode.Recommended:CrucibleMode.None);
		}
		bool v2=configuration.CrucibleLoopSelectedBoard;
		if(ImGui.Checkbox("Loop selected board",ref v2)) {
			configuration.CrucibleLoopSelectedBoard=v2;
			configuration.Save();
		}
		SettingTip("After a completed run, return to Lauda and enter the selected board again.");
		bool v3=configuration.CrucibleAutoConfigureBmrMovement;
		if(ImGui.Checkbox("Auto-configure BossMod movement (experimental)",ref v3)) {
			configuration.CrucibleAutoConfigureBmrMovement=v3;
			configuration.Save();
		}
		SettingTip("While actually inside a Crucible encounter, hands BossMod a ready-made preset (\"BeastMasterCrucible\") over its own IPC and runs /bmrai follow Slot1 then /bmrai on for you, so its AI handles dodging and staying in range of your target without you having to switch it on by hand - the same mechanism OccultHelper uses for its own auto-play. Movement only: BMH never picks targets for you, so this does not touch BossMod's targeting either. The moment you leave the encounter it runs /bmrai off and restores whatever preset you had active before. Requires BossmodReborn (Tekz Fork).\n\nOn by default; only ever acts inside a Crucible board.");
		ImGui.TextWrapped("Last result: "+CrucibleAutoPreset.LastResult);
		if(ImGui.SmallButton("Copy Crucible diagnostics")) {
			ImGui.SetClipboardText(CrucibleDiagnosticsReport());
		}
		SettingTip("Copies a short report (about 25 short lines) for troubleshooting Crucible automation: plugin versions, whether BossMod is answering, and what it reports for the current fight. It contains no character name, world, IDs or positions.");
		DrawCrucibleChores(280f);
		if(!v&&!configuration.CrucibleLevelUpBeasts) {
			ImGui.TextWrapped("Uses whatever team is already loaded in Team Composition and starts straight away. Build your team once first; an empty team stops the run.");
		}
		if(ImGui.Button("Start entry")) {
			crucibleService.Start(currentItem,v);
		}
		ImGui.EndDisabled();
		ImGui.SameLine();
		ImGui.BeginDisabled(!crucibleService.IsRunning);
		if(ImGui.Button("Stop entry")) {
			crucibleService.Stop();
		}
		ImGui.EndDisabled();
		ImGui.Spacing();
		ImGui.TextWrapped(crucibleService.Status);
		ImGui.Separator();
		DrawCrucibleBoardAssist();
		if(ImGui.CollapsingHeader("HUD items and loot debugging")) {
			CrucibleDiagnostics diagnostics=crucibleService.Diagnostics;
			ImGui.TextWrapped("Crucible Items: 10 slots (user-observed). Beast gear has separate slots and passive effects; it is not usable. Occupancy and stack counts still need mapping; recognised names are not occupied-slot counts.");
			ImGui.TextWrapped("Direct-use mapping: add a Crucible Item to a hotbar, capture Before, use that hotbar slot, then capture After and save the report. Captures include command types, IDs and icons for populated slots. Pause board assist while recording. For context-menu comparison, enable events, capture Before, right-click one Crucible Item in XBMContentsMainHUD and capture the open submenu; select Use, answer its target/confirmation if needed, then capture After. Gear only offers Discard and must not be used. Repeat in combat and outside combat. Also capture loot with a full inventory or one free slot. Save diagnostic report below.");
			bool v4=diagnostics.RecordingEvents;
			if(ImGui.Checkbox("Record item/window events",ref v4)) {
				diagnostics.SetEventRecording(v4);
			}
			if(ImGui.Button("Capture Before##Items")) {
				diagnostics.CaptureItems("Before");
			}
			ImGui.SameLine();
			if(ImGui.Button("Capture After##Items")) {
				diagnostics.CaptureItems("After");
			}
			ImGui.SameLine();
			if(ImGui.Button("Copy HUD/loot capture")) {
				ImGui.SetClipboardText(diagnostics.ItemSnapshot);
			}
			ImU8String text=new ImU8String(17,1);
			text.AppendLiteral("Recorded events: ");
			text.AppendFormatted(diagnostics.EventCount);
			ImGui.TextDisabled(text);
			if(ImGui.BeginChild("ItemEventPreview",new Vector2(0f,140f),border: true)) {
				ImGui.TextUnformatted(diagnostics.EventPreview);
			}
			ImGui.EndChild();
			if(ImGui.BeginChild("ItemWindowDump",new Vector2(0f,240f),border: true)) {
				ImGui.TextUnformatted(diagnostics.ItemSnapshot);
			}
			ImGui.EndChild();
		}
		ImGui.Separator();
		ImGui.TextDisabled("Lauda: Central Shroud / Bentbranch Meadows (25.25, -6.00, 64.85)");
		if(ImGui.CollapsingHeader("Live Crucible window inspection")) {
			if(ImGui.Button("Inspect windows")) {
				crucibleService.Diagnostics.Capture("Window inspection");
				crucibleInspection=crucibleService.Diagnostics.LastSnapshot;
			}
			ImGui.SameLine();
			if(ImGui.Button("Copy inspection")) {
				ImGui.SetClipboardText(crucibleInspection);
			}
			ImGui.TextWrapped("Stop entry first, start the event trace, then manually hover and click a board once. Capture before and after selection, then save or copy the diagnostic report. The trace observes events without clicking anything.");
			CrucibleDiagnostics diagnostics2=crucibleService.Diagnostics;
			bool v5=diagnostics2.RecordingEvents;
			if(ImGui.Checkbox("Record XBM UI events",ref v5)) {
				diagnostics2.SetEventRecording(v5);
			}
			ImU8String text2=new ImU8String(39,1);
			text2.AppendLiteral("Captured events: ");
			text2.AppendFormatted(diagnostics2.EventCount);
			text2.AppendLiteral(" (newest 500 retained)");
			ImGui.TextDisabled(text2);
			if(ImGui.BeginChild("CrucibleEventTrace",new Vector2(0f,160f),border: true)) {
				ImGui.TextUnformatted(diagnostics2.EventPreview);
			}
			ImGui.EndChild();
			if(ImGui.Button("Capture window snapshot")) {
				diagnostics2.Capture("Board/window inspection");
				crucibleInspection=diagnostics2.LastSnapshot;
			}
			if(ImGui.BeginChild("CrucibleWindowDump",new Vector2(0f,260f),border: true)) {
				ImGui.TextUnformatted(crucibleInspection);
			}
			ImGui.EndChild();
		}
		if(ImGui.CollapsingHeader("Fight team")) {
			CruciblePreparationService preparation=crucibleService.Preparation;
			bool v6=preparation.Automatic;
			if(ImGui.Checkbox("Automatically select team and commence battle",ref v6)) {
				preparation.Automatic=v6;
			}
			ImGui.TextWrapped("While inside the duty, prepares for the displayed enemy when both fight windows open, verifies three team slots, then commences. After the transition and familiar summon, sends Snarl once. Runs even with this panel closed.");
			if(ImGui.Button("Read fight and team")) {
				preparation.Refresh();
			}
			CrucibleFightState state=preparation.State;
			if(!state.Valid) {
				ImGui.TextWrapped(state.Error);
			}
			else {
				ImU8String text3=new ImU8String(30,2);
				text3.AppendLiteral("Displayed enemy: ");
				text3.AppendFormatted(state.Enemy);
				text3.AppendLiteral(" | Weakness: ");
				text3.AppendFormatted(string.Join(", ",state.WeaknessTypes));
				ImGui.TextWrapped(text3);
				ImGui.TextWrapped("Suggestions cover the enemy currently displayed in Board Layout. Primary weakness matches rank above secondary matches; remaining places use health and keep existing selections where possible. Other bosses and ability synergies are not yet scored.");
				CrucibleFamiliar[] array=CrucibleTeamPlanner.Suggest(state);
				ImGui.TextWrapped("Suggested: "+string.Join(", ",array.Select((CrucibleFamiliar f) => f.Name)));
				ImGui.TextWrapped("Selected: "+string.Join(", ",from f in state.Familiars
					where f.Slot<3
					orderby f.Slot
					select $"{f.Slot+1}: {f.Name}"));
				ImGui.BeginDisabled(preparation.Running||array.Length!=3);
				if(ImGui.Button("Apply suggested team")) {
					preparation.ApplySuggested();
				}
				ImGui.EndDisabled();
				ImGui.SameLine();
				if(ImGui.Button("Stop team selection")) {
					preparation.Stop();
				}
				ImGui.BeginDisabled(preparation.Running||Enumerable.Count(state.Familiars,(CrucibleFamiliar f) => f.Slot<3&&f.HP!=0)!=3);
				if(ImGui.Button("Commence Battle##BMH")) {
					preparation.Commence();
				}
				ImGui.EndDisabled();
				foreach(CrucibleFamiliar familiar in state.Familiars) {
					ImU8String text4=new ImU8String(30,5);
					text4.AppendFormatted(familiar.Name);
					text4.AppendLiteral(": HP ");
					text4.AppendFormatted(familiar.HP);
					text4.AppendLiteral("/");
					text4.AppendFormatted(familiar.MaxHP);
					text4.AppendLiteral(" | Primary ");
					text4.AppendFormatted(string.Join(", ",familiar.Profile.PrimaryDamage));
					text4.AppendLiteral(" | Secondary ");
					text4.AppendFormatted(string.Join(", ",familiar.Profile.SecondaryDamage));
					ImGui.TextWrapped(text4);
				}
			}
			ImGui.TextWrapped(preparation.Status);
			if(ImGui.Button("Copy all familiar abilities")) {
				ImGui.SetClipboardText(FamiliarCombatData.DescribeAll());
			}
			if(ImGui.TreeNode("All familiar abilities")) {
				foreach(FamiliarCombatProfile value in FamiliarCombatData.All.Values) {
					ImU8String label=new ImU8String(2,2);
					label.AppendFormatted(value.CatalogueId);
					label.AppendLiteral(": ");
					label.AppendFormatted(value.Name);
					if(ImGui.TreeNode(label)) {
						ImGui.TextWrapped("Primary: "+value.Primary);
						ImGui.TextWrapped("Secondary: "+value.Secondary);
						ImU8String text5=new ImU8String(34,2);
						text5.AppendLiteral("Ultimate action ");
						text5.AppendFormatted(value.UltimateAction);
						text5.AppendLiteral(" | Release action ");
						text5.AppendFormatted(value.ReleaseAction);
						ImGui.TextDisabled(text5);
						ImGui.TreePop();
					}
				}
				ImGui.TreePop();
			}
		}
		if(ImGui.CollapsingHeader("Fight preparation: weaknesses and familiars")) {
			CrucibleDiagnostics diagnostics3=crucibleService.Diagnostics;
			ImGui.TextWrapped("Reads XBMStageDetailList (Board Layout) and XBMPetParty (Team Composition). Capture on the pad, select your three familiars manually, and capture again before Commence Battle. Record XBM UI events above to trace selection and the battle button.");
			bool v7=diagnostics3.WatchFightPreparation;
			if(ImGui.Checkbox("Capture fight window changes",ref v7)) {
				diagnostics3.WatchFightPreparation=v7;
			}
			if(ImGui.Button("Capture fight preparation")) {
				diagnostics3.CaptureFightPreparation();
			}
			ImGui.SameLine();
			if(ImGui.Button("Copy fight windows")) {
				ImGui.SetClipboardText(diagnostics3.FightSnapshot);
			}
			ImGui.TextWrapped("Captures raw values, text, icons, tree rows and control state. Weakness meanings and selected-team slots still need verification before automatic team selection can use them.");
			if(ImGui.BeginChild("FightPreparationDump",new Vector2(0f,260f),border: true)) {
				ImGui.TextUnformatted(diagnostics3.FightSnapshot);
			}
			ImGui.EndChild();
		}
		if(ImGui.CollapsingHeader("In-duty board and route debugging")) {
			CrucibleDiagnostics diagnostics4=crucibleService.Diagnostics;
			ImGui.TextWrapped("Record while walking through the duty. Mark the entrance, pads, fights, treasure chests, campsites and exits. Each marker captures your position, current target, loaded objects and XBM window state. Coordinates are world X/Y/Z; markers do not start navigation.");
			bool v8=diagnostics4.RecordingRoute;
			if(ImGui.Checkbox("Record duty route (every 2 seconds)",ref v8)) {
				diagnostics4.SetRouteRecording(v8);
			}
			ImGui.InputText("Marker label",ref crucibleMarker,120);
			if(ImGui.Button("Capture marker")) {
				diagnostics4.Capture(string.IsNullOrWhiteSpace(crucibleMarker)?"Unlabelled marker":crucibleMarker);
			}
			string[] array2=new string[5] { "Entrance", "Fight", "Treasure", "Campsite", "Transition" };
			foreach(string text6 in array2) {
				ImGui.SameLine();
				if(ImGui.SmallButton(text6)) {
					diagnostics4.Capture(text6);
				}
			}
			ImU8String text7=new ImU8String(89,2);
			text7.AppendLiteral("Route samples: ");
			text7.AppendFormatted(diagnostics4.RouteCount);
			text7.AppendLiteral("; labelled snapshots: ");
			text7.AppendFormatted(diagnostics4.MarkerCount);
			text7.AppendLiteral(" (newest 120 of each retained; save before clearing)");
			ImGui.TextDisabled(text7);
			if(ImGui.BeginChild("CrucibleDutyDump",new Vector2(0f,260f),border: true)) {
				ImGui.TextUnformatted(diagnostics4.LastSnapshot);
			}
			ImGui.EndChild();
		}
		ImGui.Separator();
		if(ImGui.Button("Copy diagnostic report")) {
			ImGui.SetClipboardText(crucibleService.Diagnostics.Report());
		}
		ImGui.SameLine();
		if(ImGui.Button("Save diagnostic report")) {
			crucibleService.Diagnostics.Save();
		}
		ImGui.SameLine();
		if(ImGui.Button("Clear diagnostics")) {
			crucibleService.Diagnostics.Clear();
		}
		ImGui.TextWrapped(crucibleService.Diagnostics.ExportStatus);
	}
	private void DrawCrucibleBeastRankFallback() {
		if(!ImGui.CollapsingHeader("Manual rank fallback##BmhBeastRankFallback")) {
			return;
		}
		ImGui.TextWrapped("Only used for a familiar the Bestiary detail pane has not been read for yet this session. Once the plugin learns a rank from the game it replaces this fallback for that familiar.");
		ImGui.SetNextItemWidth(80f);
		ImGui.InputInt("No.##BmhFallbackId",ref crucibleFallbackCatalogueId);
		ImGui.SameLine();
		ImGui.SetNextItemWidth(80f);
		ImGui.InputInt("Rank##BmhFallbackRank",ref crucibleFallbackRank);
		ImGui.SameLine();
		if(ImGui.Button("Set##BmhFallbackSet")) {
			int key=Math.Clamp(crucibleFallbackCatalogueId,1,50);
			int value=Math.Max(1,crucibleFallbackRank);
			configuration.CrucibleBeastRankFallback[key]=value;
			configuration.Save();
		}
		int? num=null;
		foreach(var (num4,value2) in configuration.CrucibleBeastRankFallback.OrderBy((KeyValuePair<int,int> kv) => kv.Key)) {
			bool flag=configuration.CrucibleBeastRankLearned.ContainsKey(CrucibleBeastRank.MonsterIdFor(num4));
			ImGui.TextUnformatted($"No. {num4}: rank {value2}"+(flag?" (superseded by a learned rank)":string.Empty));
			ImGui.SameLine();
			ImU8String label=new ImU8String(25,1);
			label.AppendLiteral("Remove##BmhFallbackRemove");
			label.AppendFormatted(num4);
			if(ImGui.SmallButton(label)) {
				num=num4;
			}
		}
		if(num.HasValue) {
			int valueOrDefault=num.GetValueOrDefault();
			configuration.CrucibleBeastRankFallback.Remove(valueOrDefault);
			configuration.Save();
		}
	}
	private void DrawCrucibleClickRecorder() {
		if(!ImGui.CollapsingHeader("Click recorder##BmhClickRecorder")) {
			return;
		}
		CrucibleClickRecorder shared=CrucibleClickRecorder.Shared;
		ImGui.TextWrapped("Buying from the shop has failed repeatedly against a guessed click. Record a real one instead: open the shop, press Record, buy one item by hand, then press Stop. Automatic buying replays that captured click against whichever row it wants, so it no longer has to guess how the window expects to be told.");
		ImGui.SetNextItemWidth(260f);
		ImGui.Combo("Window##BmhRecordAddon",ref crucibleRecordAddon,CrucibleRecordableLabels,CrucibleRecordableLabels.Length);
		string text=CrucibleRecordableAddons[Math.Clamp(crucibleRecordAddon,0,CrucibleRecordableAddons.Length-1)];
		if(shared.IsRecording) {
			ImU8String label=new ImU8String(30,1);
			label.AppendLiteral("Stop recording ");
			label.AppendFormatted(shared.Watching);
			label.AppendLiteral("##BmhRecordStop");
			if(ImGui.Button(label)) {
				shared.Stop();
				configuration.CrucibleRecordedClick=shared.Serialize() ?? string.Empty;
				configuration.Save();
			}
		}
		else {
			ImU8String label2=new ImU8String(23,1);
			label2.AppendLiteral("Record ");
			label2.AppendFormatted((text=="*")?"ALL windows":text);
			label2.AppendLiteral("##BmhRecordStart");
			if(ImGui.Button(label2)) {
				shared.Start(text);
			}
		}
		ImGui.SameLine();
		if(ImGui.Button("Clear##BmhRecordClear")) {
			shared.Clear();
		}
		ImGui.SameLine();
		if(ImGui.Button("Copy##BmhRecordCopy")) {
			ImGui.SetClipboardText(shared.Report());
		}
		ImGui.SameLine();
		if(ImGui.Button("Save to file##BmhRecordSave")) {
			crucibleTracePath=SaveTrace(shared.Report());
		}
		if(crucibleTracePath.Length>0) {
			ImGui.SameLine();
			if(ImGui.Button("Copy path##BmhRecordCopyPath")) {
				ImGui.SetClipboardText(crucibleTracePath);
			}
			ImU8String text2=new ImU8String(7,1);
			text2.AppendLiteral("Saved: ");
			text2.AppendFormatted(crucibleTracePath);
			ImGui.TextWrapped(text2);
		}
		CrucibleRecordedClick lastSelection=shared.LastSelection;
		if(lastSelection!=null) {
			ImGui.SameLine();
			if(ImGui.Button("Replay last##BmhRecordReplay")) {
				shared.Replay(lastSelection);
			}
		}
		ImU8String text3=new ImU8String(20,2);
		text3.AppendFormatted(shared.Count);
		text3.AppendLiteral(" event(s) recorded. ");
		text3.AppendFormatted(shared.Status);
		ImGui.TextDisabled(text3);
		ImGui.TextWrapped((lastSelection==null)?"No click recorded yet.":$"Last click: {lastSelection}");
		ImGui.Separator();
		ImGui.TextWrapped("Familiar item trace. Start this, then - in a fight - use a potion, tome or buff on a familiar entirely by hand, all the way through the recipient window and any confirmation. Stop, then Save. Send the file: it carries the windows involved, their structure, and the exact events with their payloads.");
		if(shared.IsRecording&&shared.Watching=="XBM*") {
			if(ImGui.Button("Stop trace##BmhTraceStop")) {
				shared.Stop();
			}
		}
		else if(ImGui.Button("Start familiar item trace##BmhTraceStart")) {
			shared.Clear();
			shared.Start("XBM*");
		}
		ImGui.SameLine();
		if(ImGui.Button("Save trace##BmhTraceSave")) {
			crucibleTracePath=SaveTrace(shared.Report());
		}
		if(crucibleTracePath.Length>0) {
			ImGui.SameLine();
			if(ImGui.Button("Copy path##BmhTraceCopyPath")) {
				ImGui.SetClipboardText(crucibleTracePath);
			}
			ImU8String text4=new ImU8String(7,1);
			text4.AppendLiteral("Trace: ");
			text4.AppendFormatted(crucibleTracePath);
			ImGui.TextWrapped(text4);
		}
		DrawLevelDebugger();
	}
	private void SelectCrucibleMode(CrucibleMode mode) {
		configuration.CrucibleRecommendedTeam=mode==CrucibleMode.Recommended;
		configuration.CrucibleLevelUpBeasts=mode==CrucibleMode.LevelUp;
		configuration.CrucibleSuggestedTeamMode=mode==CrucibleMode.Suggested;
		configuration.Save();
	}
	private static bool ReactionHelperLoaded() {
		return DalamudApi.PluginInterface.InstalledPlugins.Any((IExposedPlugin p) => p.IsLoaded&&p.InternalName.Equals("ReactionHelper",StringComparison.OrdinalIgnoreCase));
	}
	private void DrawReactionHelperWarning() {
		if(ImGui.BeginPopupModal("##suggestedteammodenorh",ImGuiWindowFlags.AlwaysAutoResize)) {
			ImGui.TextColored(new Vector4(1f,0.85f,0.3f,1f),"Reaction Helper is not installed or not enabled.");
			ImGui.TextUnformatted("Without it, BMH's own built-in planning (CrucibleEnmityPlan/CrucibleFightPlan) still");
			ImGui.TextUnformatted("picks the team and runs each fight, same as before Reaction Helper existed - nothing");
			ImGui.TextUnformatted("stops working. Reaction Helper's timeline profiles add more detailed per-fight plans");
			ImGui.TextUnformatted("on top (Borrow timing, tank buster prep, targeting priority) for whichever fights you");
			ImGui.TextUnformatted("have both installed it AND ticked below.");
			ImGui.Spacing();
			ImGui.TextUnformatted("Installing it alone is not enough: for each fight you want it to run, tick 'Let");
			ImGui.TextUnformatted("Reaction Helper run this fight' under that fight below (or use the board-wide button");
			ImGui.TextUnformatted("above the fight list) - otherwise BMH keeps planning that fight itself as usual.");
			ImGui.Spacing();
			if(ImGui.Button("Install Reaction Helper",new Vector2(180f,0f))) {
				DalamudApi.PluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.AllPlugins,"ReactionHelper");
			}
			ImGui.SameLine();
			if(ImGui.Button("OK",new Vector2(120f,0f))) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		}
	}
	private void DrawCrucibleChores(float width) {
		bool v=configuration.CrucibleRepairEnabled;
		if(ImGui.Checkbox("Repair Gear Before Entry",ref v)) {
			configuration.CrucibleRepairEnabled=v;
			configuration.Save();
		}
		SettingTip("Before each entry (every loop too), repairs your own gear with Dark Matter if any piece is below the percentage below. Skips with a chat note if it cannot.");
		int v2=configuration.CrucibleRepairThreshold;
		ImGui.TextUnformatted("Repair Below %");
		ImGui.SameLine();
		ImGui.SetNextItemWidth(width*0.5f);
		if(ImGui.SliderInt("##BmhRepairBelow",ref v2,5,90)) {
			configuration.CrucibleRepairThreshold=v2;
			configuration.Save();
		}
		IReadOnlyList<CrucibleFood> readOnlyList=CrucibleChores.Foods();
		uint chosen=configuration.CrucibleFoodItemId;
		string value=((chosen==0)?"None":(readOnlyList.FirstOrDefault((CrucibleFood f) => f.ItemId==chosen)?.Name ?? $"Item {chosen} (not in bag)"));
		ImGui.SetNextItemWidth(width);
		ImU8String label="##BmhFoodPick";
		ImU8String previewValue=new ImU8String(6,1);
		previewValue.AppendLiteral("Food: ");
		previewValue.AppendFormatted(value);
		if(ImGui.BeginCombo(label,previewValue)) {
			if(ImGui.Selectable("None",chosen==0)) {
				configuration.CrucibleFoodItemId=0u;
				configuration.Save();
			}
			foreach(CrucibleFood item in readOnlyList) {
				ImU8String label2=new ImU8String(8,3);
				label2.AppendFormatted(item.Name);
				label2.AppendLiteral(" x");
				label2.AppendFormatted(item.Count);
				label2.AppendLiteral("##food");
				label2.AppendFormatted(item.ItemId);
				if(ImGui.Selectable(label2,item.ItemId==chosen)) {
					configuration.CrucibleFoodItemId=item.ItemId;
					configuration.Save();
				}
			}
			ImGui.EndCombo();
		}
		SettingTip("Eat this food before each entry when the Well Fed buff has less than the minutes below left (or is missing). Only food in your bag is listed.");
		int v3=configuration.CrucibleFoodMinMinutes;
		ImGui.TextUnformatted("Renew Under (min)");
		ImGui.SameLine();
		ImGui.SetNextItemWidth(width*0.5f);
		if(ImGui.SliderInt("##BmhFoodMin",ref v3,1,60)) {
			configuration.CrucibleFoodMinMinutes=v3;
			configuration.Save();
		}
		ImU8String text=new ImU8String(19,1);
		text.AppendLiteral("Well Fed: ");
		text.AppendFormatted(CrucibleChores.FoodMinutesLeft(),"0.#");
		text.AppendLiteral(" min left");
		ImGui.TextDisabled(text);
	}
	private void DrawLevelDebugger() {
		ImGui.Separator();
		levelDebugger.Tick();
		if(!ImGui.CollapsingHeader("Level Up Beasts debugger##BmhLevelDebug")) {
			return;
		}
		ImGui.TextWrapped("Run the Level Up Beasts steps by hand, in order, with Team Composition open. Nothing here moves the character. Save the log and send its path.");
		ImGui.TextUnformatted("1. Team window");
		if(ImGui.Button("Read team##BmhLdTeam")) {
			levelDebugger.ReadTeam();
		}
		ImGui.SameLine();
		if(ImGui.Button("Show plan##BmhLdPlan")) {
			levelDebugger.ShowPlan();
		}
		ImGui.TextUnformatted("2. Master's Bestiary");
		if(ImGui.Button("Open##BmhLdOpen")) {
			levelDebugger.OpenBestiary();
		}
		ImGui.SameLine();
		if(ImGui.Button("Close##BmhLdClose")) {
			levelDebugger.CloseBestiary();
		}
		ImGui.SameLine();
		if(ImGui.Button("Page 1##BmhLdP1")) {
			levelDebugger.SelectPage(1);
		}
		ImGui.SameLine();
		if(ImGui.Button("Page 2##BmhLdP2")) {
			levelDebugger.SelectPage(2);
		}
		ImGui.SameLine();
		if(ImGui.Button("Read grid##BmhLdGrid")) {
			levelDebugger.ReadGrid();
		}
		ImGui.TextUnformatted("3. Rank and exp");
		ImGui.SetNextItemWidth(90f);
		ImGui.InputInt("No.##BmhLdId",ref levelDebugId);
		levelDebugId=Math.Clamp(levelDebugId,1,50);
		ImGui.SameLine();
		if(ImGui.Button("Read this familiar##BmhLdRead")) {
			levelDebugger.ReadPane(levelDebugId);
		}
		ImGui.SameLine();
		if(ImGui.Button("Scan current page##BmhLdScan")) {
			levelDebugger.ScanPage();
		}
		ImGui.TextUnformatted("4. One click, watched");
		if(ImGui.Button("Click this familiar once and watch##BmhLdClick")) {
			levelDebugger.ClickAndWatch(levelDebugId);
		}
		ImGui.SameLine();
		ImGui.TextDisabled("(toggles it - press again to undo)");
		ImGui.Spacing();
		if(ImGui.Button("Save log to file##BmhLdSave")) {
			levelDebugger.Save();
		}
		ImGui.SameLine();
		if(ImGui.Button("Copy log##BmhLdCopy")) {
			ImGui.SetClipboardText(levelDebugger.Text);
		}
		ImGui.SameLine();
		if(ImGui.Button("Clear log##BmhLdClear")) {
			levelDebugger.Clear();
		}
		if(levelDebugger.SavedPath.Length>0) {
			ImGui.SameLine();
			if(ImGui.Button("Copy path##BmhLdPath")) {
				ImGui.SetClipboardText(levelDebugger.SavedPath);
			}
			ImU8String text=new ImU8String(7,1);
			text.AppendLiteral("Saved: ");
			text.AppendFormatted(levelDebugger.SavedPath);
			ImGui.TextWrapped(text);
		}
		ImU8String text2=new ImU8String(10,2);
		text2.AppendFormatted(levelDebugger.Lines);
		text2.AppendLiteral(" line(s). ");
		text2.AppendFormatted(levelDebugger.Status);
		ImGui.TextDisabled(text2);
		ImGui.Separator();
		ImGui.TextUnformatted("Familiars as the plugin sees them (one row each; a re-read updates its row)");
		if(ImGui.Button("Clear table##BmhLdClearTable")) {
			levelDebugger.ClearTable();
		}
		ImGui.SameLine();
		if(ImGui.Button("Forget saved ranks/exp##BmhLdForget")) {
			levelDebugger.ForgetSaved();
		}
		SettingTip("Empties the ranks and exp the plugin remembered from earlier scans, so the next scan rebuilds them from nothing.");
		List<CrucibleLevelDebugger.Row> list=levelDebugger.Table();
		if(ImGui.BeginTable("##BmhLdTable",8,ImGuiTableFlags.Borders|ImGuiTableFlags.SizingStretchProp|ImGuiTableFlags.RowBg|ImGuiTableFlags.ScrollY,new Vector2(0f,240f))) {
			ImGui.TableSetupColumn("No.");
			ImGui.TableSetupColumn("Name");
			ImGui.TableSetupColumn("Rank");
			ImGui.TableSetupColumn("Exp %");
			ImGui.TableSetupColumn("Exp raw");
			ImGui.TableSetupColumn("Grid");
			ImGui.TableSetupColumn("Team");
			ImGui.TableSetupColumn("Source / time");
			ImGui.TableHeadersRow();
			foreach(CrucibleLevelDebugger.Row item in list) {
				ImGui.TableNextRow();
				ImGui.TableNextColumn();
				ImGui.TextUnformatted(item.No.ToString());
				ImGui.TableNextColumn();
				ImGui.TextUnformatted(item.Name);
				ImGui.TableNextColumn();
				ImGui.TextUnformatted(item.Rank?.ToString() ?? "?");
				ImGui.TableNextColumn();
				ImGui.TextUnformatted(item.Exp?.ToString() ?? "?");
				ImGui.TableNextColumn();
				ImGui.TextUnformatted(item.ExpRaw);
				ImGui.TableNextColumn();
				bool? registered=item.Registered;
				string text3;
				if(registered.HasValue) {
					text3=((registered==true)?"yes":"no");
				}
				else {
					text3="-";
				}
				ImGui.TextUnformatted(text3);
				ImGui.TableNextColumn();
				registered=item.InTeam;
				string text4;
				if(registered.HasValue) {
					text4=((registered==true)?"yes":"no");
				}
				else {
					text4="-";
				}
				ImGui.TextUnformatted(text4);
				ImGui.TableNextColumn();
				DateTime? seen=item.Seen;
				string text5;
				if(seen.HasValue) {
					DateTime valueOrDefault=seen.GetValueOrDefault();
					text5=$"{item.Source} {valueOrDefault:HH:mm:ss}";
				}
				else {
					text5=item.Source;
				}
				ImGui.TextUnformatted(text5);
			}
			ImGui.EndTable();
		}
		string text6=levelDebugger.Text;
		if(text6.Length>6000) {
			string text7=text6;
			text6="..."+text7.Substring(text7.Length-6000);
		}
		ImGui.BeginChild("##BmhLdLog",new Vector2(0f,260f),border: true);
		ImGui.TextUnformatted(text6);
		if(ImGui.GetScrollY()>=ImGui.GetScrollMaxY()-5f) {
			ImGui.SetScrollHereY(1f);
		}
		ImGui.EndChild();
	}
	private static string SaveTrace(string trace) {
		try {
			string pluginConfigDirectory=DalamudApi.PluginInterface.GetPluginConfigDirectory();
			Directory.CreateDirectory(pluginConfigDirectory);
			string text=Path.Combine(pluginConfigDirectory,$"crucible-trace-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
			File.WriteAllText(text,trace);
			DalamudApi.ChatGui.Print("[Beast Master Helper] Familiar item trace saved to "+text);
			return text;
		}
		catch(Exception ex) {
			DalamudApi.Log.Warning(ex,"Could not save the familiar item trace.");
			return "could not save: "+ex.Message;
		}
	}
	private string CrucibleDiagnosticsReport() {
		BossModBridge bossMod=rotationRunner.BossMod;
		StringBuilder sb=new StringBuilder();
		sb.AppendLine("### BMH Crucible Diagnostics");
		sb.AppendLine();
		Section("Environment");
		Row("BMH version",typeof(BeastmasterPlugin).Assembly.GetName().Version?.ToString() ?? "?");
		Row("Game language",Safe(() => DalamudApi.DataManager.Language.ToString()));
		Row("Operating system",Safe(() => RuntimeInformation.OSDescription));
		Row("Running under Wine",Safe(() => (Registry.CurrentUser.OpenSubKey("Software\\Wine")==null)?"no":"yes (Linux/Mac)"));
		Row("Dalamud version",Safe(() => typeof(IDalamudPluginInterface).Assembly.GetName().Version?.ToString()));
		List<IExposedPlugin> source=DalamudApi.PluginInterface.InstalledPlugins.Where((IExposedPlugin p) => p.InternalName.Contains("BossMod",StringComparison.OrdinalIgnoreCase)).ToList();
		List<string> list=(from p in source
			where p.IsLoaded
			select $"{p.InternalName} {p.Version}").ToList();
		List<string> list2=(from p in source
			where !p.IsLoaded
			select $"{p.InternalName} {p.Version}").ToList();
		Row("BossMod loaded",(list.Count>0)?string.Join(", ",list):"none");
		Row("BossMod not loaded",(list2.Count>0)?string.Join(", ",list2):"-");
		Row("vnavmesh",navigationService.IsVnavmeshInstalled?"loaded":"missing");
		Row("Lifestream",navigationService.IsLifestreamInstalled?"loaded":"missing");
		Section("Game state");
		uint territoryType=DalamudApi.ClientState.TerritoryType;
		Row("Territory",$"{territoryType} ({(IsCrucibleTerritory(territoryType)?"a Crucible board":"not a Crucible board")})");
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		IGameObject target=DalamudApi.TargetManager.Target;
		Row("Job",(localPlayer==null)?"not loaded":$"{localPlayer.ClassJob.RowId} ({((localPlayer.ClassJob.RowId==43)?"Beastmaster":"not Beastmaster")})");
		Row("Level",(localPlayer==null)?"-":localPlayer.Level.ToString());
		Row("In combat",YesNo(DalamudApi.Condition[ConditionFlag.InCombat]));
		Row("In duty",YesNo(DalamudApi.Condition[ConditionFlag.BoundByDuty]));
		Row("Target",(target==null)?"none":$"{target.ObjectKind}{((target is IBattleNpc battleNpc)?$", {battleNpc.BattleNpcKind}":"")}, {((!(target is IBattleChara battleChara))?"-":(battleChara.IsDead?"dead":"alive"))}");
		Section("BMH settings");
		Row("Auto-configure BossMod",OnOff(configuration.CrucibleAutoConfigureBmrMovement));
		Row("Rotation",OnOff(configuration.RotationEnabled));
		List<string> list3=new List<string>();
		if(configuration.CrucibleRecommendedTeam) {
			list3.Add("recommended");
		}
		if(configuration.CrucibleSuggestedTeamMode) {
			list3.Add("suggested");
		}
		if(configuration.CrucibleLevelUpBeasts) {
			list3.Add("leveling");
		}
		Row("Team mode",(list3.Count>0)?string.Join(" + ",list3):"none");
		Row("Recommended / suggested / leveling",$"{OnOff(configuration.CrucibleRecommendedTeam)} / {OnOff(configuration.CrucibleSuggestedTeamMode)} / {OnOff(configuration.CrucibleLevelUpBeasts)}");
		Section("BossMod");
		Row("Answering",YesNo(bossMod.IsInstalled));
		Row("In Crucible fight",YesNo(bossMod.InCrucibleEncounter));
		string value;
		if(bossMod.InEncounter&&bossMod.EncounterName.Length>0) {
			value=bossMod.EncounterName;
		}
		else {
			value=(bossMod.InEncounter?"active":"none");
		}
		Row("Module",value);
		Row("AI on",YesNo(bossMod.AiEnabled));
		string text=bossMod.ReadSetting("BossModuleConfig","maturity","MinMaturity");
		string text2=bossMod.ReadSetting("ZoneModuleConfig","maturity","MinMaturity");
		string value2;
		if(text==null) {
			value2="unreadable";
		}
		else {
			value2=((text2==null)?text:(text+" (zone modules / Full duty automation: "+text2+")"));
		}
		Row("Current BMR Modules Setting",value2);
		Row("Needed BMR Modules Setting","WIP");
		string value3;
		if(text==null) {
			value3="unknown";
		}
		else {
			value3=((text.Equals("WIP",StringComparison.OrdinalIgnoreCase)||text.Equals("Dummy",StringComparison.OrdinalIgnoreCase))?"yes":"NO - (Crucible modules are WIP; set the bmr modules' minimal maturity to WIP)");
		}
		Row("Crucible modules can load",value3);
		string aiPresetName=bossMod.AiPresetName;
		Row("Preset BMH sets",$"`{"BeastMasterCrucible"}` (exists: {YesNo(bossMod.GetPreset("BeastMasterCrucible")!=null)})");
		Row("AI preset",(aiPresetName.Length>0)?("`"+aiPresetName+"`"):"none");
		Row("BMH preset active",YesNo(CrucibleAutoPreset.IsActive));
		Row("Last result",CrucibleAutoPreset.LastResult);
		Section("Live state");
		Row("Preferred target",YesNo(bossMod.PreferredTargetID!=0));
		Row("Hold actions",YesNo(bossMod.ShouldHoldActions));
		Row("Facing restricted",YesNo(bossMod.IsFacingRestricted));
		Row("Danger zones",bossMod.DangerZones.ToString());
		Row("Next damage in",FormatSeconds(bossMod.NextDamageIn));
		Row("Rotation status",rotationRunner.StatusText);
		return sb.ToString();
		static string Cell(string text4) {
			string text3=text4.Replace("|","\\|").Replace("\r"," ").Replace("\n"," ")
				.Trim();
			if(text3.Length!=0) {
				return text3;
			}
			return "-";
		}
		static string OnOff(bool flag) {
			if(!flag) {
				return "off";
			}
			return "on";
		}
		void Row(string key,string value4) {
			StringBuilder stringBuilder=sb;
			StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(7,2,stringBuilder);
			handler.AppendLiteral("| ");
			handler.AppendFormatted(key);
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(Cell(value4));
			handler.AppendLiteral(" |");
			stringBuilder.AppendLine(ref handler);
		}
		void Section(string title) {
			if(sb.Length>0) {
				sb.AppendLine();
			}
			StringBuilder stringBuilder=sb;
			StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(5,1,stringBuilder);
			handler.AppendLiteral("#### ");
			handler.AppendFormatted(title);
			stringBuilder.AppendLine(ref handler);
			sb.AppendLine();
			sb.AppendLine("| Item | Value |");
			sb.AppendLine("| --- | --- |");
		}
		static string YesNo(bool flag) {
			if(!flag) {
				return "no";
			}
			return "yes";
		}
	}
	private static string Safe(Func<string?> read) {
		try {
			string text=read();
			return string.IsNullOrWhiteSpace(text)?"unknown":text;
		}
		catch(Exception) {
			return "unknown";
		}
	}
	private static string FormatSeconds(float seconds) {
		if(!(seconds>=1000f)) {
			return $"{seconds:0.0}s";
		}
		return "-";
	}
	private static bool IsCrucibleTerritory(uint territory) {
		if(territory-1339<=4) {
			return true;
		}
		return false;
	}
	public PluginUI(BeastmasterConfiguration configuration,BeastmasterProgressService progressService,BeastmasterQuestService questService,BeastmasterNavigationService navigationService,BeastmasterDebugDataService debugDataService,BeastmasterAutoCaptureService autoCaptureService,BeastmasterRotationRunner rotationRunner,BeastmasterCrucibleService crucibleService,CrucibleBoardService crucibleBoardService) {
		this.crucibleService=crucibleService;
		levelDebugger=new CrucibleLevelDebugger(configuration);
		this.crucibleBoardService=crucibleBoardService;
		this.configuration=configuration;
		this.progressService=progressService;
		this.questService=questService;
		this.navigationService=navigationService;
		this.debugDataService=debugDataService;
		this.autoCaptureService=autoCaptureService;
		this.rotationRunner=rotationRunner;
	}
	public void OpenMainWindow() {
		isMainWindowOpen=true;
	}
	private static int PlayerLevel() {
		return DalamudApi.ObjectTable.LocalPlayer?.Level ?? 0;
	}
	public void Draw() {
		RefreshGaugeSnapshot();
		PollHotkeys();
		ArenaOverlayRenderer.Draw(configuration);
		DrawOverlays();
		if(isMainWindowOpen) {
			ImGui.SetNextWindowSize(new Vector2(900f,600f),ImGuiCond.FirstUseEver);
			ImGui.SetNextWindowSizeConstraints(new Vector2(700f,460f),new Vector2(float.MaxValue,float.MaxValue));
			ImU8String name=new ImU8String(21,1);
			name.AppendLiteral("Beast Master Helper v");
			name.AppendFormatted(GetType().Assembly.GetName().Version);
			if(!ImGui.Begin(name,ref isMainWindowOpen)) {
				ImGui.End();
				return;
			}
			DrawMainShell();
			ImGui.End();
		}
	}
	private void DrawAutoCaptureOverlay() {
		if(!autoCaptureService.IsEnabled||!DalamudApi.ClientState.IsLoggedIn||DalamudApi.ObjectTable.LocalPlayer==null||DalamudApi.PlayerState.ClassJob.RowId!=43) {
			return;
		}
		ImGui.SetNextWindowPos(new Vector2(20f,180f),ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowBgAlpha(0.9f);
		if(!ImGui.Begin("##BeastmasterAutoCaptureOverlay",ImGuiWindowFlags.NoNav|ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoScrollWithMouse|ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoFocusOnAppearing)) {
			ImGui.End();
			return;
		}
		DrawAutoOutputHeader();
		if(ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)&&ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
			configuration.SelectedMainSection="settings";
			configuration.Save();
			isMainWindowOpen=true;
		}
		if(autoOutputCollapsed) {
			ImGui.End();
			return;
		}
		ImGui.Separator();
		ImGui.TextColored(new Vector4(0.35f,0.85f,0.55f,1f),autoCaptureService.StatusText);
		ImU8String text=new ImU8String(13,1);
		text.AppendLiteral("Next action: ");
		text.AppendFormatted(autoCaptureService.NextActionName);
		ImGui.Text(text);
		if(configuration.ShowGaugeInOverlay&&!string.IsNullOrWhiteSpace(autoCaptureService.NextActionReason)) {
			ImU8String text2=new ImU8String(8,1);
			text2.AppendLiteral("Reason: ");
			text2.AppendFormatted(autoCaptureService.NextActionReason);
			ImGui.TextDisabled(text2);
		}
		if(configuration.ShowGaugeInOverlay) {
			DrawOverlayGaugeSummary();
		}
		DrawOverlayTargetStatus();
		if(configuration.ShowGaugeInOverlay) {
			DrawOverlayAdvancedCandidates();
		}
		ImGui.Separator();
		bool v=autoCaptureService.TryCapture;
		if(ImGui.Checkbox("Attempt capture",ref v)) {
			autoCaptureService.SetTryCapture(v);
		}
		ImGui.SameLine();
		DrawCompactCaptureHpThreshold();
		DrawOverlayAdvancedActionToggles();
		if(ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)&&ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
			configuration.SelectedMainSection="settings";
			configuration.Save();
			isMainWindowOpen=true;
		}
		ImGui.End();
	}
	private void DrawAutoOutputHeader() {
		ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(1f,0.85f,0.35f,1f));
		ImGui.AlignTextToFramePadding();
		ImGui.TextUnformatted("Beastmaster ACR");
		ImGui.PopStyleColor();
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip(autoOutputCollapsed?"Left-click to expand the overlay":"Left-click to collapse the overlay");
		}
		if(ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
			autoOutputCollapsed=!autoOutputCollapsed;
		}
		ImGui.SameLine();
		DrawOverlayStatusBadge(autoCaptureService.IsEnabled?"Auto":"Off",autoCaptureService.IsEnabled?new Vector4(0.2f,0.42f,0.28f,1f):new Vector4(0.3f,0.3f,0.34f,1f),autoCaptureService.IsEnabled?new Vector4(0.45f,1f,0.58f,1f):new Vector4(0.7f,0.7f,0.75f,1f));
	}
	private static void DrawOverlayStatusBadge(string label,Vector4 background,Vector4 textColor) {
		ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,8f);
		ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(7f,2f));
		ImGui.PushStyleColor(ImGuiCol.Button,background);
		ImGui.PushStyleColor(ImGuiCol.ButtonHovered,background);
		ImGui.PushStyleColor(ImGuiCol.ButtonActive,background);
		ImGui.PushStyleColor(ImGuiCol.Border,new Vector4(0f,0f,0f,0f));
		ImGui.PushStyleColor(ImGuiCol.Text,textColor);
		ImGui.Button(label);
		ImGui.PopStyleColor(5);
		ImGui.PopStyleVar(2);
	}
	private void DrawMainShell() {
		if(ImGui.BeginTable("BeastmasterMainShell",2,ImGuiTableFlags.SizingStretchProp|ImGuiTableFlags.Resizable|ImGuiTableFlags.BordersInnerV)) {
			ImGui.TableSetupColumn("Navigate",ImGuiTableColumnFlags.WidthFixed,190f);
			ImGui.TableSetupColumn("Content",ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			DrawSidebar();
			ImGui.TableNextColumn();
			DrawContent();
			ImGui.EndTable();
		}
	}
	private void RefreshGaugeSnapshot() {
		if(!(DateTime.UtcNow<nextGaugeRefreshUtc)) {
			gaugeSnapshot=BeastmasterGaugeSnapshot.Read();
			nextGaugeRefreshUtc=DateTime.UtcNow.AddMilliseconds(100.0);
		}
	}
	private void DrawOverlayGaugeSummary() {
		if(gaugeSnapshot.Available) {
			BeastmasterCatalogEntry summonEntry=gaugeSnapshot.SummonEntry;
			ImGui.Text((summonEntry==null)?("Current familiar: "+((gaugeSnapshot.SummonDataId==0)?"Not summoned":gaugeSnapshot.SummonName)):$"Current familiar: {summonEntry.DisplayName} [{summonEntry.Attribute}]");
			DrawOverlayGaugeBar("Tenacity",(int)gaugeSnapshot.Tp,250f,new Vector4(0.95f,0.75f,0.2f,1f));
			DrawOverlayGaugeBar("Beast Power",(int)gaugeSnapshot.BeastPower,250f,new Vector4(0.35f,0.7f,1f,1f));
			ImU8String text=new ImU8String(42,2);
			text.AppendLiteral("Beast Heart: ");
			text.AppendFormatted(gaugeSnapshot.BeastHeartStacks);
			text.AppendLiteral(" stacks | Beast Soul: ");
			text.AppendFormatted(gaugeSnapshot.BeastSoulStacks);
			text.AppendLiteral(" stacks");
			ImGui.Text(text);
		}
	}
	private void DrawOverlayTargetStatus() {
		ImGui.Text("Current target");
		IGameObject target=DalamudApi.TargetManager.Target;
		if(!(target is IBattleChara)) {
			ImGui.SameLine();
			ImGui.TextDisabled("No valid target");
			return;
		}
		ImGui.SameLine();
		ImGui.Text(target.Name.TextValue);
		ImGui.SameLine();
		ImU8String text=new ImU8String(5,2);
		text.AppendFormatted(autoCaptureService.TargetHpPercent,"0.#");
		text.AppendLiteral("% Â· ");
		text.AppendFormatted(autoCaptureService.TargetStatus);
		ImGui.TextDisabled(text);
		if(configuration.ShowGaugeInOverlay) {
			ImU8String text2=new ImU8String(9,1);
			text2.AppendLiteral("Capture: ");
			text2.AppendFormatted(autoCaptureService.CaptureState);
			ImGui.TextDisabled(text2);
		}
	}
	private static void DrawOverlayGaugeBar(string label,float value,float maximum,Vector4 color) {
		float fraction=Math.Clamp(value/maximum,0f,1f);
		ImGui.PushStyleColor(ImGuiCol.PlotHistogram,color);
		ImGui.SetWindowFontScale(0.8f);
		float y=ImGui.GetTextLineHeight()+2f*ImGui.GetIO().FontGlobalScale;
		Vector2 sizeArg=new Vector2(-1f,y);
		ImU8String overlay=new ImU8String(2,3);
		overlay.AppendFormatted(label);
		overlay.AppendLiteral(" ");
		overlay.AppendFormatted(value,"0");
		overlay.AppendLiteral("/");
		overlay.AppendFormatted(maximum,"0");
		ImGui.ProgressBar(fraction,sizeArg,overlay);
		ImGui.SetWindowFontScale(1f);
		ImGui.PopStyleColor();
	}
	private void DrawCompactCaptureHpThreshold() {
		float v=Math.Clamp(configuration.CaptureHpThreshold,1f,100f);
		ImGui.SetNextItemWidth(120f);
		ImU8String label="##overlay-capture-threshold";
		ImU8String format=new ImU8String(1,1);
		format.AppendFormatted(v,"0");
		format.AppendLiteral("%");
		if(ImGui.SliderFloat(label,ref v,1f,100f,format)) {
			configuration.CaptureHpThreshold=v;
			configuration.Save();
		}
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Capture HP threshold: use the capture action once the target drops below this percentage");
		}
	}
	private void DrawOverlayAdvancedCandidates() {
		if(!ImGui.CollapsingHeader("Advanced action candidates##BeastmasterAdvancedCandidates")) {
			return;
		}
		if(!configuration.AdvancedActionsEnabled) {
			ImGui.TextDisabled("Advanced actions are switched off, so resources are simply held.");
			return;
		}
		BeastmasterCatalogEntry summonEntry=gaugeSnapshot.SummonEntry;
		if(summonEntry==null) {
			ImGui.TextDisabled("Current familiar not identified, so no advanced action candidates can be listed.");
			return;
		}
		Vector4 col=GetAttributeColor(summonEntry.Attribute);
		ImU8String text=new ImU8String(11,1);
		text.AppendLiteral("Attribute: ");
		text.AppendFormatted(summonEntry.Attribute);
		ImGui.TextColored(in col,text);
		DrawAdvancedCandidate(configuration.BeastHeartCooperationEnabled?"Beast Heart cooperation (yellow)":"Beast Soul cooperation (blue)",configuration.BeastHeartCooperationEnabled?(GetActionName(47093u)+" -> attribute axe"):("Attribute axe -> "+GetActionName(47093u)),configuration.BeastHeartCooperationEnabled||configuration.BeastSoulCooperationEnabled,(gaugeSnapshot.Tp>=100&&gaugeSnapshot.BeastPower>=100)?"Tenacity and Beast Power meet the basic threshold":$"Not enough resources: Tenacity {gaugeSnapshot.Tp}/100, Beast Power {gaugeSnapshot.BeastPower}/100");
		ImU8String text2=new ImU8String(37,1);
		text2.AppendLiteral("Release: adjust actions at runtime (");
		text2.AppendFormatted(configuration.AutoReleaseEnabled?"On":"Off");
		text2.AppendLiteral(")");
		ImGui.TextDisabled(text2);
		ImU8String label=new ImU8String(37,0);
		label.AppendLiteral("Use ultimate##manual-ultimate-overlay");
		if(ImGui.Button(label)) {
			autoCaptureService.TryUseUltimate();
		}
		ImGui.SameLine();
		ImGui.TextDisabled(autoCaptureService.ManualActionStatus);
	}
	private static void DrawAdvancedCandidate(string type,string actionName,bool enabled,string reason) {
		ImU8String text=new ImU8String(3,2);
		text.AppendFormatted(type);
		text.AppendLiteral("ï¼š");
		text.AppendFormatted(actionName);
		ImGui.Text(text);
		ImGui.SameLine();
		ImGui.TextDisabled(enabled?reason:"Switched off");
	}
	private void DrawSidebar() {
		ImGui.Text("Beast Master Helper");
		ImGui.TextDisabled("Beastmaster Progress Hub");
		ImGui.Separator();
		DrawSidebarLabel("Content");
		DrawSidebarButton(MainSections[0]);
		DrawSidebarButton(MainSections[1]);
		DrawSidebarButton((Key: "crucible",Label: "Crucible of the Unbroken"));
		DrawSidebarButton(MainSections[8]);
		DrawSidebarButton(MainSections[2]);
		DrawSidebarButton(MainSections[3]);
		DrawSidebarButton(MainSections[4]);
		DrawSidebarButton(MainSections[7]);
		ImGui.Separator();
		DrawSidebarLabel("Tool");
		DrawSidebarButton(MainSections[5]);
		DrawSidebarButton(MainSections[6]);
	}
	private void DrawSidebarButton((string Key,string Label) section) {
		bool flag=configuration.SelectedMainSection==section.Key;
		if(flag) {
			ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(0.12f,0.23f,0.25f,1f));
		}
		ImU8String label=new ImU8String(10,2);
		label.AppendFormatted(section.Label);
		label.AppendLiteral("##section-");
		label.AppendFormatted(section.Key);
		if(ImGui.Button(label,new Vector2(ImGui.GetContentRegionAvail().X,34f))) {
			configuration.SelectedMainSection=section.Key;
			configuration.Save();
		}
		if(flag) {
			ImGui.PopStyleColor();
		}
	}
	private static void DrawSidebarLabel(string label) {
		ImGui.Spacing();
		ImGui.TextDisabled(label);
	}
	private void DrawContent() {
		switch(configuration.SelectedMainSection) {
		case "crucible":
		DrawCrucible();
		break;
		case "quests":
		DrawQuests();
		break;
		case "catalog":
		DrawCatalog();
		break;
		case "equipment":
		DrawEquipment();
		break;
		case "commands":
		DrawCommands();
		break;
		case "auto-output":
		DrawRotationStuff();
		break;
		case "settings":
		DrawSettings();
		break;
		case "debug":
		DrawDebug();
		break;
		case "support":
		DrawSupport();
		break;
		case "boards":
		DrawCrucibleBoardOptions();
		break;
		default:
		configuration.SelectedMainSection="quests";
		DrawQuests();
		break;
		}
	}
	private void DrawQuests() {
		ImGui.Text("Beastmaster quest chain");
		ImGui.SameLine();
		if(ImGui.Button("WIKI##quest-wiki")) {
			Process.Start(new ProcessStartInfo("https://ffxiv.consolegameswiki.com/wiki/Beastmaster#Quests") {
				UseShellExecute = true
			});
		}
		ImGui.SameLine();
		float num=ImGui.CalcTextSize("Stop navigation").X+ImGui.GetStyle().FramePadding.X*2f;
		float num2=ImGui.GetWindowContentRegionMax().X-num;
		if(ImGui.GetCursorPosX()<num2) {
			ImGui.SetCursorPosX(num2);
		}
		if(ImGui.Button("Stop navigation")) {
			navigationService.Stop();
		}
		ImGui.TextDisabled("Shows progress from the client quest state and routes you to the pickup point of anything unfinished.");
		ImGui.Separator();
		IReadOnlyList<BeastmasterQuestDefinition> quests=BeastmasterQuestGuide.Quests;
		BeastmasterQuestDefinition[] source=quests.Where((BeastmasterQuestDefinition quest) => quest.RowId!=0).ToArray();
		uint[] questRowIds=source.SelectMany((BeastmasterQuestDefinition quest) => (from previous in questService.GetPrerequisites(quest.RowId)
			select previous.RowId).Append(quest.RowId)).Distinct().ToArray();
		if(DateTime.UtcNow>=nextQuestStatusRefreshUtc) {
			questService.RefreshStatuses(questRowIds);
			nextQuestStatusRefreshUtc=DateTime.UtcNow.AddSeconds(2.0);
		}
		BeastmasterQuestDefinition[] array=source.Where((BeastmasterQuestDefinition quest) => !quest.IsCrucible).ToArray();
		BeastmasterQuestDefinition[] array2=source.Where((BeastmasterQuestDefinition quest) => quest.IsCrucible).ToArray();
		int num3=array.Count((BeastmasterQuestDefinition quest) => questService.GetStatus(quest.RowId)==BeastmasterQuestStatus.Completed);
		float fraction=((array.Length==0)?0f:((float)num3/(float)array.Length));
		Vector2 sizeArg=new Vector2(-1f,0f);
		ImU8String overlay=new ImU8String(12,2);
		overlay.AppendLiteral("Job chain  ");
		overlay.AppendFormatted(num3);
		overlay.AppendLiteral("/");
		overlay.AppendFormatted(array.Length);
		ImGui.ProgressBar(fraction,sizeArg,overlay);
		if(array2.Length!=0) {
			int num4=array2.Count((BeastmasterQuestDefinition quest) => questService.GetStatus(quest.RowId)==BeastmasterQuestStatus.Completed);
			float fraction2=(float)num4/(float)array2.Length;
			Vector2 sizeArg2=new Vector2(-1f,0f);
			ImU8String overlay2=new ImU8String(19,2);
			overlay2.AppendLiteral("Crucible unlocks  ");
			overlay2.AppendFormatted(num4);
			overlay2.AppendLiteral("/");
			overlay2.AppendFormatted(array2.Length);
			ImGui.ProgressBar(fraction2,sizeArg2,overlay2);
		}
		ImGui.Spacing();
		bool v=configuration.HideCompletedQuests;
		ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(1f,0.82f,0.25f,1f));
		if(ImGui.Checkbox("Hide completed quests",ref v)) {
			configuration.HideCompletedQuests=v;
			configuration.Save();
		}
		ImGui.PopStyleColor();
		ImGui.Separator();
		foreach(BeastmasterQuestDefinition item in quests) {
			if(item.RowId==0) {
				ImU8String text=new ImU8String(21,1);
				text.AppendFormatted(item.DisplayName);
				text.AppendLiteral("  [not yet collected]");
				ImGui.Text(text);
				ImGui.TextDisabled(item.Summary);
				ImGui.BeginDisabled();
				ImGui.Button("Navigate to quest giver##issuer-pending");
				ImGui.SameLine();
				ImGui.Button("Navigate to objective##target-pending");
				ImGui.EndDisabled();
				ImGui.Separator();
				continue;
			}
			BeastmasterQuestStatus status=questService.GetStatus(item.RowId);
			bool flag=status==BeastmasterQuestStatus.Completed;
			if(flag&&configuration.HideCompletedQuests) {
				continue;
			}
			string value;
			if(flag) {
				value="Complete";
			}
			else {
				value=((status==BeastmasterQuestStatus.Accepted)?"In progress":"Not accepted");
			}
			string value2=((item.Level>0)?$"Lv{item.Level}  ":string.Empty);
			ImU8String text2=new ImU8String(4,3);
			text2.AppendFormatted(value2);
			text2.AppendFormatted(item.DisplayName);
			text2.AppendLiteral("  [");
			text2.AppendFormatted(value);
			text2.AppendLiteral("]");
			ImGui.Text(text2);
			if(!string.IsNullOrEmpty(item.Giver)) {
				ImU8String text3=new ImU8String(11,2);
				text3.AppendLiteral("    Giver: ");
				text3.AppendFormatted(item.Giver);
				text3.AppendFormatted(item.IsCrucible?"   (high-end crucible)":string.Empty);
				ImGui.TextDisabled(text3);
			}
			foreach(BeastmasterQuestPrerequisite prerequisite in questService.GetPrerequisites(item.RowId)) {
				bool flag2=questService.GetStatus(prerequisite.RowId)==BeastmasterQuestStatus.Completed;
				ImU8String text4=new ImU8String(14,1);
				text4.AppendLiteral("Prerequisite: ");
				text4.AppendFormatted(prerequisite.Name);
				ImGui.TextDisabled(text4);
				ImGui.SameLine();
				ImGui.TextColored(flag2?new Vector4(0.35f,0.8f,0.48f,1f):new Vector4(0.9f,0.32f,0.3f,1f),flag2?"[Complete]":"[Incomplete]");
			}
			if(questService.TryGetIssuerLocation(item.RowId,out BeastmasterQuestLocation location)) {
				ImU8String text5=new ImU8String(16,2);
				text5.AppendLiteral("Quest giver: ");
				text5.AppendFormatted(location.NpcName);
				text5.AppendLiteral(" - ");
				text5.AppendFormatted(location.Zone);
				ImGui.TextDisabled(text5);
				if(flag) {
					ImGui.BeginDisabled();
				}
				ImU8String label=new ImU8String(32,1);
				label.AppendLiteral("Navigate to quest giver##issuer-");
				label.AppendFormatted(item.RowId);
				if(ImGui.Button(label)) {
					navigationService.Navigate(location);
				}
				if(flag) {
					ImGui.EndDisabled();
				}
			}
			else {
				ImGui.TextDisabled("Quest giver: the client provides no usable pickup coordinates");
			}
			ImGui.SameLine();
			BeastmasterQuestLocation location2=null;
			bool flag3=status==BeastmasterQuestStatus.Accepted&&questService.TryGetCurrentTarget(item.RowId,out location2);
			if(!flag3) {
				ImGui.BeginDisabled();
			}
			ImU8String label2=new ImU8String(30,1);
			label2.AppendLiteral("Navigate to objective##target-");
			label2.AppendFormatted(item.RowId);
			if(ImGui.Button(label2)&flag3) {
				navigationService.NavigateQuestTarget(location2);
			}
			if(!flag3) {
				ImGui.EndDisabled();
				if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
					ImGui.SetTooltip("Target coordinates for the current quest step have not been collected yet. ");
				}
			}
			ImGui.Separator();
		}
	}
	private static void DrawReserved(string title) {
		ImGui.Text(title);
		ImGui.Separator();
		ImGui.TextDisabled("This entry is to be decided.");
	}
	private static void DrawCommands() {
		ImGui.Text("Commands");
		ImGui.Separator();
		string command=BeastmasterGameData.TextCommand(54u,"/bestiary");
		string text=BeastmasterGameData.TextCommand(52u,"/beastsize");
		DrawGameCommandButton("Master's Bestiary",command);
		DrawGameCommandButton("Familiar size - small",text+" all small");
		DrawGameCommandButton("Familiar size - medium",text+" all medium");
		DrawGameCommandButton("Familiar size - large",text+" all large");
	}
	private void DrawEquipment() {
		RefreshEquipmentOwnership();
		ImGui.Text("Recommended gear");
		ImGui.TextDisabled("Level 50 starter and best-in-slot gear for Beastmaster.");
		int currentItem=selectedEquipmentSet;
		ImGui.SetNextItemWidth(220f);
		if(ImGui.Combo("Gear set",ref currentItem,"Starter\0Best in slot\0")) {
			selectedEquipmentSet=currentItem;
		}
		ImGui.Separator();
		if(!ImGui.BeginTable("BeastmasterEquipmentTable",4,ImGuiTableFlags.SizingStretchProp|ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH)) {
			return;
		}
		ImGui.TableSetupColumn("Slot",ImGuiTableColumnFlags.WidthFixed,90f);
		ImGui.TableSetupColumn("Equipment",ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("Category",ImGuiTableColumnFlags.WidthFixed,70f);
		ImGui.TableSetupColumn("Owned",ImGuiTableColumnFlags.WidthFixed,210f);
		ImGui.TableHeadersRow();
		foreach(BeastmasterEquipmentEntry item in (selectedEquipmentSet==0)?BeastmasterEquipmentGuide.Level50Starter:BeastmasterEquipmentGuide.Level50BestInSlot) {
			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			ImGui.Text(item.Slot);
			ImGui.TableNextColumn();
			if(item.Name=="No equipment") {
				ImGui.TextDisabled(item.DisplayName);
			}
			else {
				ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(0.45f,0.75f,1f,1f));
				ImGui.Text(item.DisplayName);
				ImGui.PopStyleColor();
				if(ImGui.IsItemHovered()) {
					ImGui.SetTooltip("Open the wiki entry for this item");
				}
				if(ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
					OpenEquipmentWiki(item.DisplayName);
				}
			}
			ImGui.TableNextColumn();
			ImGui.TextDisabled(item.Category);
			ImGui.TableNextColumn();
			DrawEquipmentOwnership(item);
		}
		ImGui.EndTable();
		ImGui.Spacing();
		ImGui.TextDisabled((selectedEquipmentSet==0)?"Starter set: chosen for ease of acquisition and getting going quickly.":"Best in slot: chosen for peak level 50 combat stats. ");
	}
	private static void OpenEquipmentWiki(string equipmentName) {
		Process.Start(new ProcessStartInfo("https://www.garlandtools.org/db/#search/"+Uri.EscapeDataString(equipmentName)) {
			UseShellExecute = true
		});
	}
	private void DrawEquipmentOwnership(BeastmasterEquipmentEntry equipment) {
		if(equipment.Name=="No equipment") {
			ImGui.TextDisabled("-");
			return;
		}
		if(!equipmentOwnership.TryGetValue(equipment.Name,out (uint,uint,uint,uint) value)||value.Item1==0) {
			ImGui.TextDisabled("ItemId to be filled in");
			return;
		}
		uint item=value.Item2;
		uint item2=value.Item3;
		uint item3=value.Item4;
		if(item!=0) {
			Vector4 col=new Vector4(0.4f,0.9f,0.5f,1f);
			ImU8String text=new ImU8String(11,1);
			text.AppendLiteral("Equipped (");
			text.AppendFormatted(item);
			text.AppendLiteral(")");
			ImGui.TextColored(in col,text);
			ImGui.SameLine();
		}
		ImGui.TextColored((item2!=0)?new Vector4(0.4f,0.9f,0.5f,1f):new Vector4(0.65f,0.65f,0.7f,1f),(item2!=0)?$"In bags ({item2})":"Not in bags");
		ImGui.SameLine();
		ImGui.TextColored((item3!=0)?new Vector4(0.4f,0.8f,1f,1f):new Vector4(0.65f,0.65f,0.7f,1f),(item3!=0)?$"In armoury ({item3})":"Not in armoury");
	}
	private void RefreshEquipmentOwnership() {
		if(DateTime.UtcNow<nextEquipmentRefreshUtc) {
			return;
		}
		equipmentOwnership.Clear();
		foreach(BeastmasterEquipmentEntry item in ((selectedEquipmentSet==0)?BeastmasterEquipmentGuide.Level50Starter:BeastmasterEquipmentGuide.Level50BestInSlot).Where((BeastmasterEquipmentEntry item) => item.Name!="No equipment")) {
			if(item.ItemId==0) {
				equipmentOwnership[item.Name]=(0u,0u,0u,0u);
			}
			else {
				equipmentOwnership[item.Name]=(item.ItemId,CountItems(item.ItemId,new[] { GameInventoryType.EquippedItems }),CountItems(item.ItemId,InventoryTypes()),CountItems(item.ItemId,ArmoryTypes(item.Slot)));
			}
		}
		nextEquipmentRefreshUtc=DateTime.UtcNow.AddSeconds(1.0);
	}
	private static uint CountItems(uint itemId,IEnumerable<GameInventoryType> types) {
		uint num=0u;
		foreach(GameInventoryType type in types) {
			ReadOnlySpan<GameInventoryItem> inventoryItems=DalamudApi.GameInventory.GetInventoryItems(type);
			for(int i=0;i<inventoryItems.Length;i++) {
				GameInventoryItem gameInventoryItem=inventoryItems[i];
				if(!gameInventoryItem.IsEmpty&&gameInventoryItem.BaseItemId==itemId) {
					num+=(uint)gameInventoryItem.Quantity;
				}
			}
		}
		return num;
	}
	private static IEnumerable<GameInventoryType> InventoryTypes() {
		yield return GameInventoryType.Inventory1;
		yield return GameInventoryType.Inventory2;
		yield return GameInventoryType.Inventory3;
		yield return GameInventoryType.Inventory4;
	}
	private static IEnumerable<GameInventoryType> ArmoryTypes(string slot) {
		switch(slot) {
		case "Main Hand":
		yield return GameInventoryType.ArmoryMainHand;
		yield break;
		case "Off Hand":
		yield return GameInventoryType.ArmoryOffHand;
		yield break;
		case "Head":
		yield return GameInventoryType.ArmoryHead;
		yield break;
		case "Body":
		yield return GameInventoryType.ArmoryBody;
		yield break;
		case "Hands":
		yield return GameInventoryType.ArmoryHands;
		yield break;
		case "Legs":
		yield return GameInventoryType.ArmoryLegs;
		yield break;
		case "Feet":
		yield return GameInventoryType.ArmoryFeets;
		yield break;
		case "Earrings":
		yield return GameInventoryType.ArmoryEar;
		yield break;
		case "Necklace":
		yield return GameInventoryType.ArmoryNeck;
		yield break;
		case "Bracelets":
		yield return GameInventoryType.ArmoryWrist;
		yield break;
		}
		if(slot.StartsWith("Ring",StringComparison.Ordinal)) {
			yield return GameInventoryType.ArmoryRings;
		}
		else if(slot=="Soul Crystal") {
			yield return GameInventoryType.ArmorySoulCrystal;
		}
	}
	private static void DrawGameCommandButton(string label,string command) {
		if(ImGui.Button(label)&&!GameCommandService.Execute(command)) {
			DalamudApi.ChatGui.Print("[Beast Master Helper] Could not run "+command+".");
		}
	}
	private void DrawCatalog() {
		ImGui.Text("Beast catalogue");
		ImGui.SameLine();
		if(ImGui.Button("WIKI")) {
			Process.Start(new ProcessStartInfo("https://ffxiv.consolegameswiki.com/wiki/Beastmaster") {
				UseShellExecute = true
			});
		}
		ImGui.SameLine();
		float num=ImGui.CalcTextSize("Stop navigation").X+ImGui.GetStyle().FramePadding.X*2f;
		float num2=ImGui.GetWindowContentRegionMax().X-num;
		if(ImGui.GetCursorPosX()<num2) {
			ImGui.SetCursorPosX(num2);
		}
		if(ImGui.Button("Stop navigation##catalog-stop-navigation")) {
			navigationService.Stop();
		}
		ImGui.TextDisabled("Records automatically on a successful capture; you can also tick entries by hand per character.");
		ImGui.Separator();
		IReadOnlyList<BeastmasterCatalogEntry> entries=BeastmasterCatalog.Entries;
		bool v=configuration.SortCatalogByLocation;
		bool v2=configuration.HideCapturedBeasts;
		ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(1f,0.82f,0.25f,1f));
		if(ImGui.Checkbox("Sort by map",ref v)) {
			configuration.SortCatalogByLocation=v;
			configuration.Save();
		}
		ImGui.SameLine();
		bool v3=configuration.SortCatalogByLevel;
		if(ImGui.Checkbox("Sort by level",ref v3)) {
			configuration.SortCatalogByLevel=v3;
			configuration.Save();
		}
		ImGui.SameLine();
		if(ImGui.Checkbox("Hide captured familiars",ref v2)) {
			configuration.HideCapturedBeasts=v2;
			configuration.Save();
		}
		ImGui.PopStyleColor();
		IEnumerable<BeastmasterCatalogEntry> source=entries.AsEnumerable();
		if(configuration.HideCapturedBeasts) {
			source=source.Where((BeastmasterCatalogEntry e) => !progressService.IsCompleted(e.Key));
		}
		uint territoryType=DalamudApi.ClientState.TerritoryType;
		string currentMapName=(DalamudApi.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryType,out var row)?row.Map.Value.PlaceName.Value.Name.ExtractText():string.Empty);
		int num3=Enumerable.Count(entries,(BeastmasterCatalogEntry entry) => progressService.IsCompleted(entry.Key));
		float fraction=(float)num3/(float)entries.Count;
		Vector2 sizeArg=new Vector2(-1f,0f);
		ImU8String overlay=new ImU8String(1,2);
		overlay.AppendFormatted(num3);
		overlay.AppendLiteral("/");
		overlay.AppendFormatted(entries.Count);
		ImGui.ProgressBar(fraction,sizeArg,overlay);
		ImGui.Spacing();
		if(!ImGui.BeginTable("BeastmasterCatalogTable",8,ImGuiTableFlags.SizingStretchProp|ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.ScrollY,new Vector2(0f,0f))) {
			return;
		}
		ImGui.TableSetupColumn("Complete",ImGuiTableColumnFlags.WidthFixed,46f);
		ImGui.TableSetupColumn("No. / Familiar",ImGuiTableColumnFlags.WidthFixed,150f);
		ImGui.TableSetupColumn("Attribute",ImGuiTableColumnFlags.WidthFixed,48f);
		ImGui.TableSetupColumn("Action",ImGuiTableColumnFlags.WidthFixed,118f);
		ImGui.TableSetupColumn("Level",ImGuiTableColumnFlags.WidthFixed,50f);
		ImGui.TableSetupColumn("Zone / Duty",ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("Position",ImGuiTableColumnFlags.WidthFixed,112f);
		ImGui.TableSetupColumn("Navigate",ImGuiTableColumnFlags.WidthFixed,105f);
		ImGui.TableHeadersRow();
		bool flag=progressService.CurrentCharacterKey.Length>0;
		IEnumerable<BeastmasterCatalogEntry> enumerable;
		if(configuration.SortCatalogByLocation) {
			enumerable=source.OrderBy((BeastmasterCatalogEntry entry) => !entry.DisplayLocation.Equals(currentMapName,StringComparison.Ordinal)).ThenBy((BeastmasterCatalogEntry entry) => entry.DisplayLocation,StringComparer.Ordinal).ThenBy((BeastmasterCatalogEntry entry) => (!configuration.SortCatalogByLevel)?int.MaxValue:GetCatalogMinimumLevel(entry.Level))
				.ThenBy((BeastmasterCatalogEntry entry) => entry.Number);
		}
		else {
			enumerable=((!configuration.SortCatalogByLevel)?source.OrderBy((BeastmasterCatalogEntry entry) => entry.Number):(from entry in source
				orderby GetCatalogMinimumLevel(entry.Level),entry.Number
				select entry));
		}
		foreach(BeastmasterCatalogEntry item in enumerable) {
			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			bool v4=progressService.IsCompleted(item.Key);
			if(!flag) {
				ImGui.BeginDisabled();
			}
			ImU8String label=new ImU8String(19,1);
			label.AppendLiteral("##catalog-complete-");
			label.AppendFormatted(item.Number);
			if(ImGui.Checkbox(label,ref v4)) {
				progressService.SetCompleted(item.Key,v4);
			}
			if(!flag) {
				ImGui.EndDisabled();
			}
			ImGui.TableNextColumn();
			ImU8String text=new ImU8String(2,2);
			text.AppendFormatted(item.Number);
			text.AppendLiteral(". ");
			text.AppendFormatted(item.DisplayName);
			ImGui.Text(text);
			ImGui.TableNextColumn();
			ImGui.TextColored(GetAttributeColor(item.Attribute),item.Attribute.ToString());
			ImGui.TableNextColumn();
			ImU8String text2=new ImU8String(3,2);
			text2.AppendFormatted(GetActionName(item.UltimateActionId));
			text2.AppendLiteral(" /\n");
			text2.AppendFormatted(GetActionName(item.ReleaseActionId));
			ImGui.TextWrapped(text2);
			if(ImGui.IsItemHovered()) {
				ImGui.BeginTooltip();
				DrawActionTooltip("Ultimate",item.UltimateActionId);
				DrawActionTooltip("Release",item.ReleaseActionId);
				ImGui.EndTooltip();
			}
			ImGui.TableNextColumn();
			ImGui.TextUnformatted(item.Level);
			ImGui.TableNextColumn();
			ImGui.TextUnformatted(item.DisplayLocation);
			ImGui.TableNextColumn();
			ImGui.TextUnformatted(GetCatalogCoordinate(item));
			ImGui.TableNextColumn();
			if(item.LocationType==BeastmasterCatalogLocationType.Field&&item.MapX.HasValue&&item.MapY.HasValue) {
				bool flag2=BeastmasterCaptureTargeting.NeedsHigherLevel(item,PlayerLevel());
				ImGui.BeginDisabled(flag2);
				ImU8String label2=new ImU8String(14,2);
				label2.AppendFormatted(flag2?"Need Higher Lv":"Navigate");
				label2.AppendLiteral("##catalog-nav-");
				label2.AppendFormatted(item.Number);
				if(ImGui.SmallButton(label2)) {
					navigationService.Navigate(item);
				}
				ImGui.EndDisabled();
				if(flag2&&ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
					ImU8String tooltip=new ImU8String(43,2);
					tooltip.AppendLiteral("Its level range starts at ");
					tooltip.AppendFormatted(BeastmasterCaptureTargeting.LowestLevel(item.Level));
					tooltip.AppendLiteral("; you are level ");
					tooltip.AppendFormatted(PlayerLevel());
					tooltip.AppendLiteral(".");
					ImGui.SetTooltip(tooltip);
				}
			}
			else if(item.LocationType==BeastmasterCatalogLocationType.Duty) {
				ImU8String label3=new ImU8String(19,1);
				label3.AppendLiteral("Duty##catalog-duty-");
				label3.AppendFormatted(item.Number);
				if(ImGui.SmallButton(label3)) {
					navigationService.OpenDutyFinder(item);
				}
			}
		}
		ImGui.EndTable();
	}
	private static string GetCatalogCoordinate(BeastmasterCatalogEntry entry) {
		switch(entry.LocationType) {
		case BeastmasterCatalogLocationType.Field:
		if(entry.MapX.HasValue&&entry.MapY.HasValue) {
			return $"X:{entry.MapX:0.#}, Y:{entry.MapY:0.#}";
		}
		break;
		case BeastmasterCatalogLocationType.Duty:
		return "Duty";
		case BeastmasterCatalogLocationType.Starting:
		return "Starting";
		}
		return "Unknown";
	}
	private static int GetCatalogMinimumLevel(string level) {
		if(!int.TryParse(new string(level.TakeWhile(char.IsDigit).ToArray()),out var result)) {
			return int.MaxValue;
		}
		return result;
	}
	private static Vector4 GetAttributeColor(BeastmasterAttribute attribute) {
		return attribute switch {
			BeastmasterAttribute.Fierce => new Vector4(0.95f,0.35f,0.3f,1f),
			BeastmasterAttribute.Sturdy => new Vector4(0.35f,0.65f,1f,1f),
			BeastmasterAttribute.Arcane => new Vector4(1f,0.82f,0.25f,1f),
			BeastmasterAttribute.Winged => new Vector4(0.4f,0.9f,0.5f,1f),
			_ => new Vector4(0.6f,0.6f,0.6f,1f),
		};
	}
	private static string GetActionName(uint actionId) {
		if(!DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId,out var row)) {
			return actionId.ToString();
		}
		return row.Name.ExtractText();
	}
	private static void DrawActionTooltip(string type,uint actionId) {
		if(!DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId,out var row)) {
			ImU8String text=new ImU8String(21,2);
			text.AppendFormatted(type);
			text.AppendLiteral(": ActionId ");
			text.AppendFormatted(actionId);
			text.AppendLiteral(" not found");
			ImGui.TextDisabled(text);
			return;
		}
		ImU8String text2=new ImU8String(3,2);
		text2.AppendFormatted(type);
		text2.AppendLiteral("ï¼š");
		text2.AppendFormatted(row.Name.ExtractText());
		ImGui.Text(text2);
		ImU8String text3=new ImU8String(41,4);
		text3.AppendLiteral("ActionId: ");
		text3.AppendFormatted(actionId);
		text3.AppendLiteral(" - Level: ");
		text3.AppendFormatted(row.ClassJobLevel);
		text3.AppendLiteral(" - Range: ");
		text3.AppendFormatted(row.Range);
		text3.AppendLiteral(" - Radius: ");
		text3.AppendFormatted(row.EffectRange);
		ImGui.TextDisabled(text3);
	}
	private void DrawSettings() {
		ImGui.Text("Settings");
		ImGui.TextDisabled("Rotation tuning has its own page now - see \"Rotation Stuff\" -> Settings.");
		ImGui.Separator();
		ImGui.Spacing();
		ImGui.Text("Required plugins");
		ImGui.Spacing();
		DrawDependency("vnavmesh",navigationService.IsVnavmeshInstalled,"Same-map pathing and movement");
		DrawDependency("Lifestream",navigationService.IsLifestreamInstalled,"Cross-map teleport");
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		ImGui.Text("General settings");
		ImGui.Spacing();
		int v=configuration.UpdateIntervalMs;
		ImGui.SetNextItemWidth(200f);
		if(ImGui.SliderInt("Update interval (ms)##tick",ref v,0,500)) {
			configuration.UpdateIntervalMs=v;
			configuration.Save();
		}
		SettingTip("How often the rotation runs, in milliseconds. 0 = every frame. A higher number does less work per second (fewer frame drops) but reacts a little later; 10-25 is barely noticeable. Ctrl+click the slider to type a number.");
		DrawSettingCheckbox("Hide completed quests","The quests tab lists only unfinished Beastmaster quests.","HideCompletedQuests",configuration.HideCompletedQuests);
		DrawSettingCheckbox("Record captures from chat","Mark a catalogue entry complete when the befriend message appears.","AutoCompleteCatalogFromChat",configuration.AutoCompleteCatalogFromChat);
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		ImGui.Text("Navigation settings");
		ImGui.Spacing();
		DrawSettingCheckbox("Flight navigation","Allow vnavmesh to use flight paths.","UseFlightNavigation",configuration.UseFlightNavigation);
		DrawSettingCheckbox("Set map flag","Also place a map flag when navigation starts.","SetFlagOnNavigation",configuration.SetFlagOnNavigation);
		DrawSettingCheckbox("Show navigation log","Print navigation start and failure messages to chat.","ShowNavigationLogs",configuration.ShowNavigationLogs);
	}
	private void DrawSupport() {
		ImGui.Text("Issue Support & Ko-Fi/Tips");
		ImGui.Separator();
		ImGui.Spacing();
		DrawSupportSection();
	}
	private static void DrawSupportSection() {
		ImGui.Text("Issue support");
		ImGui.TextWrapped("Found a bug or have a suggestion? Open an issue on GitHub and say which plugin it is about.");
		if(ImGui.Button("Report an issue on GitHub##support")) {
			Process.Start(new ProcessStartInfo("https://github.com/t3knical/DalamudPlugins/issues") {
				UseShellExecute = true
			});
		}
		ImGui.SameLine();
		if(ImGui.Button("Copy link##supportcopy")) {
			ImGui.SetClipboardText("https://github.com/t3knical/DalamudPlugins/issues");
		}
		ImGui.TextDisabled("https://github.com/t3knical/DalamudPlugins/issues");
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		ImGui.Text("Ko-fi / tips");
		ImGui.TextWrapped("Enjoying the plugin? Ko-fi donations help cover the AI token costs behind building and fixing these plugins. Thank you - it is genuinely appreciated!");
		if(ImGui.Button("Tip on Ko-fi##kofi")) {
			Process.Start(new ProcessStartInfo("https://ko-fi.com/t3knical") {
				UseShellExecute = true
			});
		}
		ImGui.SameLine();
		if(ImGui.Button("Copy link##kofcopy")) {
			ImGui.SetClipboardText("https://ko-fi.com/t3knical");
		}
		ImGui.TextDisabled("https://ko-fi.com/t3knical");
	}
	private void DrawRecommendedDpsButton() {
		if(!recommendedDpsConfirming) {
			if(ImGui.Button("Reset rotation to recommended (max DPS)")) {
				recommendedDpsConfirming=true;
			}
			SettingTip("Puts every rotation setting back to the highest-damage configuration: axes at the 100 TP floor, Rally and Rallying Cheer at 3 stacks, Tempered Release over Borrow, Parting Blow on and waiting for Lingering Vantage, familiar cycling at 45s in slot order, Shield Charge for gap closing only, and Beast Mode on with Quelling Wave kept to range.\n\nLeaves alone: whether the rotation and auto-attack are running, all capture settings, the overlays, the arena tool, and your per-encounter Shield Charge block list. None of those affect damage.");
			return;
		}
		ImGui.TextColored(new Vector4(1f,0.8f,0.4f,1f),"Overwrite your rotation settings?");
		if(ImGui.Button("Yes, reset them")) {
			configuration.ApplyRecommendedDps();
			recommendedDpsConfirming=false;
			DalamudApi.ChatGui.Print("[Beast Master Helper] Rotation settings reset to the recommended max-DPS values.");
		}
		ImGui.SameLine();
		if(ImGui.Button("Cancel")) {
			recommendedDpsConfirming=false;
		}
	}
	private static void RotationLabel(string text) {
		ImGui.AlignTextToFramePadding();
		ImGui.TextUnformatted(text);
		ImGui.SameLine(260f);
		ImGui.SetNextItemWidth(200f);
	}
	private void DrawRotationSettings() {
		ImGui.Text("Rotation");
		byte b=DalamudApi.ObjectTable.LocalPlayer?.Level ?? 0;
		ImGui.TextDisabled((b>0)?$"Scales to your level ({b}). Actions you have not learned are simply skipped.":"Scales to your level. Actions you have not learned are simply skipped.");
		DrawRecommendedDpsButton();
		ImGui.Spacing();
		if(ImGui.BeginChild("##bmh-rotation-sections",new Vector2(190f,300f),border: true)) {
			string[] rotationSections=RotationSections;
			foreach(string text in rotationSections) {
				bool flag=text==rotationSection;
				if(flag) {
					ImGui.PushStyleColor(ImGuiCol.Button,ImGui.GetStyle().Colors[22]);
				}
				ImU8String label=new ImU8String(12,2);
				label.AppendFormatted(text);
				label.AppendLiteral("##bmhrotsec_");
				label.AppendFormatted(text);
				if(ImGui.Button(label,new Vector2(-1f,26f))) {
					rotationSection=text;
				}
				if(flag) {
					ImGui.PopStyleColor();
				}
			}
		}
		ImGui.EndChild();
		ImGui.SameLine();
		if(ImGui.BeginChild("##bmh-rotation-rows",Vector2.Zero,border: true)) {
			switch(rotationSection) {
			case "Core":
			DrawRotationCoreSection();
			break;
			case "Parting Blow & Cycling":
			DrawRotationPartingBlowSection();
			break;
			case "Shield Charge":
			DrawRotationShieldChargeSection();
			break;
			case "Familiar Selection":
			DrawRotationFamiliarSection();
			break;
			case "Advanced":
			DrawRotationAdvancedSection();
			break;
			}
		}
		ImGui.EndChild();
	}
	private void DrawRotationCoreSection() {
		RotationLabel("AI Assist");
		bool v=configuration.RotationEnabled;
		if(ImGui.Checkbox("##bmh-ai-assist",ref v)) {
			SetAiAssist(v);
		}
		SettingTip("Let the plugin play Beastmaster for you. Switching this on also switches off the older auto-attack loop on the Rotation Stuff page's Main tab: both press actions independently on the same GCD, so running the two together makes them fight each other rather than acting twice as fast.\n\nPresses the next action on your current target. It never picks targets for you.");
		RotationLabel("Opener");
		if(ImGui.Checkbox("##bmh-opener",ref configuration.RotationUseOpener)) {
			configuration.Save();
		}
		SettingTip("Run the opener from a standing start instead of dropping straight into filler.");
		RotationLabel("AoE");
		if(ImGui.Checkbox("##bmh-aoe",ref configuration.RotationUseAoe)) {
			configuration.Save();
		}
		SettingTip("Use area actions once enough enemies are clustered on the target.");
		if(configuration.RotationUseAoe) {
			RotationLabel("Enemies for AoE");
			int v2=configuration.RotationAoeThreshold;
			if(ImGui.SliderInt("##bmh-aoe-threshold",ref v2,2,10)) {
				configuration.RotationAoeThreshold=v2;
				configuration.Save();
			}
		}
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		RotationLabel("Cooldowns");
		if(ImGui.Checkbox("##bmh-cooldowns",ref configuration.RotationUseCooldowns)) {
			configuration.Save();
		}
		SettingTip("Allow Rally, Rallying Cheer, Shield Charge and One with Nature.");
		RotationLabel("Rally");
		if(ImGui.Checkbox("##bmh-rally",ref configuration.RotationUseRally)) {
			configuration.Save();
		}
		RotationLabel("Rallying Cheer");
		if(ImGui.Checkbox("##bmh-rallying-cheer",ref configuration.RotationUseRallyingCheer)) {
			configuration.Save();
		}
		RotationLabel("Power actions (Trick / axes)");
		if(ImGui.Checkbox("##bmh-power-actions",ref configuration.RotationUsePowerActions)) {
			configuration.Save();
		}
		string summary=RotationToggleOverrides.Summary;
		if(summary.Length>0) {
			ImU8String text=new ImU8String(20,1);
			text.AppendLiteral("Reaction overrides: ");
			text.AppendFormatted(summary);
			ImGui.TextWrapped(text);
		}
		if(configuration.RotationUseCooldowns) {
			ImGui.Spacing();
			RotationLabel("Rally at stacks");
			int v3=configuration.RotationRallyMinStacks;
			if(ImGui.SliderInt("##bmh-rally-stacks",ref v3,1,3)) {
				configuration.RotationRallyMinStacks=v3;
				configuration.Save();
			}
			SettingTip("Mastered Instinct to bank before Rally fires. Rally gives 40 TP plus 70 per stack on a 120s cooldown, so 3 stacks is 250 - a full bar from nothing - and 1 stack is 110, throwing most of the cooldown away. Burn ignores this.");
			RotationLabel("Rallying Cheer at stacks");
			int v4=configuration.RotationCheerMinStacks;
			if(ImGui.SliderInt("##bmh-cheer-stacks",ref v4,1,3)) {
				configuration.RotationCheerMinStacks=v4;
				configuration.Save();
			}
			SettingTip("Same arithmetic for your familiar: 30 Familiar TP plus 70 per Natural Instinct stack, 120s cooldown.");
			ImGui.Spacing();
			ImGui.TextDisabled("One with Nature - granted by every summon, used once");
			RotationLabel("Borrow");
			bool v5=configuration.RotationUseOneWithNature&&configuration.OneWithNature==OneWithNatureChoice.Borrow;
			if(ImGui.Checkbox("##bmh-borrow",ref v5)) {
				configuration.SetNatureToggle(OneWithNatureChoice.Borrow,v5);
				configuration.Save();
			}
			RotationLabel("Tempered Release");
			bool v6=configuration.RotationUseOneWithNature&&configuration.OneWithNature==OneWithNatureChoice.TemperedRelease;
			if(ImGui.Checkbox("##bmh-tempered-release",ref v6)) {
				configuration.SetNatureToggle(OneWithNatureChoice.TemperedRelease,v6);
				configuration.Save();
			}
			SettingTip("Only one can be on; both can be off. Explicit choice takes precedence over automatic plans and familiar preferences.");
		}
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		RotationLabel("Burn");
		if(ImGui.Checkbox("##bmh-burn",ref configuration.RotationBurn)) {
			configuration.Save();
		}
		SettingTip("Spend everything as it comes up instead of holding resources for the wheel. Lower potency overall - for a dying boss or the end of a pull.");
	}
	private void DrawRotationPartingBlowSection() {
		ImGui.TextDisabled("Parting Blow (10s) - always dismisses your familiar");
		ImGui.Spacing();
		RotationLabel("Use for AoE damage");
		if(ImGui.Checkbox("##bmh-pb-aoe",ref configuration.RotationAllowPartingBlow)) {
			configuration.Save();
		}
		SettingTip("1000 potency in a wide radius. Your familiar retreats afterwards, which is why it is off by default and why the two guards below apply.");
		RotationLabel("Cycle through all three familiars");
		if(ImGui.Checkbox("##bmh-cycle-familiars",ref configuration.RotationCycleFamiliars)) {
			configuration.Save();
		}
		SettingTip("Dismiss the current familiar with Parting Blow when its turn is done and summon the next in kinship order.\n\nEach Battlehorn has its own cooldown group and only 2s of recast, which starts when the familiar retreats - so the loop is limited by Parting Blow's 10s, not by the horns. Needs at least two identified slots, and Auto rather than a pinned slot.\n\nThis does NOT require the AoE option above.");
		if(configuration.RotationAllowPartingBlow||configuration.RotationCycleFamiliars) {
			ImGui.Spacing();
			ImGui.TextDisabled("Before Parting Blow may be used, either way:");
			RotationLabel("Familiar out at least");
			int v=configuration.RotationCycleDwellSeconds;
			if(ImGui.SliderInt("##bmh-cycle-dwell",ref v,10,180)) {
				configuration.RotationCycleDwellSeconds=v;
				configuration.Save();
			}
			SettingTip("Seconds the familiar must have been summoned before it can be spent. Applies to the AoE use as well as cycling (BMH's own Parting Blows; Reaction Helper's familiar-cycling profile waits 30s on its own).\n\nIn the Crucible a Battlehorn comes back about 70s after its familiar leaves, so sending familiars away faster than this is what leaves you with none out.");
			RotationLabel("And its Beast Power is spent");
			if(ImGui.Checkbox("##bmh-cycle-spent-power",ref configuration.RotationCycleWaitForSpentPower)) {
				configuration.Save();
			}
			SettingTip("Beast Power leaves with the familiar and is what Trick spends, so dismissing a full one throws away a Trick. With this on it waits until under 100.");
			RotationLabel("And Lingering Vantage is up");
			if(ImGui.Checkbox("##bmh-cycle-vantage",ref configuration.RotationPartingBlowNeedsVantage)) {
				configuration.Save();
			}
			SettingTip("Lingering Vantage takes Parting Blow from 1,000 to 1,500 potency, and both One with Nature options grant it. Only applies from level 18, where there is a way to earn it - below that it is ignored rather than blocking Parting Blow.");
			int value=BeastmasterActions.UnlockLevel(44891u);
			if(!BeastmasterActions.IsUnlocked(44891u,DalamudApi.ObjectTable.LocalPlayer?.Level ?? 1)) {
				Vector4 col=new Vector4(1f,0.8f,0.4f,1f);
				ImU8String text=new ImU8String(31,1);
				text.AppendLiteral("Parting Blow unlocks at level ");
				text.AppendFormatted(value);
				text.AppendLiteral(".");
				ImGui.TextColored(in col,text);
			}
		}
	}
	private void DrawRotationShieldChargeSection() {
		ImGui.TextDisabled("Shield Charge (60s)");
		ImGui.Spacing();
		RotationLabel("Use as a gap closer");
		if(ImGui.Checkbox("##bmh-sc-gapclose",ref configuration.ShieldChargeGapClose)) {
			configuration.Save();
		}
		SettingTip("Charge to a target you cannot reach. This is what the ability is for.");
		if(configuration.ShieldChargeBlockedEncounters.Count>0) {
			ImGui.Spacing();
			ImGui.TextDisabled("Never charge in these fights");
			SettingTip("Add a fight from the rotation panel while you are in it. This only stops the charge being used to close distance: with 'Also use on packs' on, a pack around the target can still be charged for damage. Every fight not listed here still uses the normal BossMod safety check.");
			string text=null;
			foreach(string shieldChargeBlockedEncounter in configuration.ShieldChargeBlockedEncounters) {
				ImGui.Bullet();
				ImGui.SameLine();
				ImGui.TextUnformatted(shieldChargeBlockedEncounter);
				ImGui.SameLine();
				ImU8String label=new ImU8String(14,1);
				label.AppendLiteral("Remove##charge");
				label.AppendFormatted(shieldChargeBlockedEncounter);
				if(ImGui.SmallButton(label)) {
					text=shieldChargeBlockedEncounter;
				}
			}
			if(text!=null) {
				configuration.ShieldChargeBlockedEncounters.Remove(text);
				configuration.Save();
			}
		}
		if(configuration.ShieldChargeGapClose) {
			RotationLabel("Charge beyond");
			float v=configuration.ShieldChargeMinDistance;
			if(ImGui.SliderFloat("##bmh-sc-mindist",ref v,3f,20f,"%.0fy")) {
				configuration.ShieldChargeMinDistance=v;
				configuration.Save();
			}
			SettingTip("Measured edge to edge, so a big target counts as closer than its centre suggests.");
		}
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		RotationLabel("Also use on packs");
		if(ImGui.Checkbox("##bmh-sc-aoe",ref configuration.ShieldChargeAoe)) {
			configuration.Save();
		}
		SettingTip("Also fire it on cooldown for damage when enough enemies are gathered around the enemy you would charge to - the charge lands on your target and hits everything within 6y of it. Off by default: it is a gap closer first, and spending it for damage leaves you walking to the next target.");
		if(configuration.ShieldChargeAoe) {
			RotationLabel("Enemies needed");
			int v2=configuration.ShieldChargeAoeCount;
			if(ImGui.SliderInt("##bmh-sc-aoe-count",ref v2,2,12)) {
				configuration.ShieldChargeAoeCount=v2;
				configuration.Save();
			}
			RotationLabel("Within");
			float v3=configuration.ShieldChargeAoeRadius;
			if(ImGui.SliderFloat("##bmh-sc-aoe-radius",ref v3,3f,15f,"%.0fy")) {
				configuration.ShieldChargeAoeRadius=v3;
				configuration.Save();
			}
			SettingTip("Counted around your target, where the charge lands, not around you. The charge covers 6y, so use 6 or more here; 3 or 4 enemies makes a good pack.");
		}
	}
	private void DrawRotationFamiliarSection() {
		RotationLabel("Auto-summon a familiar");
		if(ImGui.Checkbox("##bmh-autosummon",ref configuration.RotationAutoSummon)) {
			configuration.Save();
		}
		SettingTip("Keep a familiar out at all times. \"Cycle through all three familiars\" and its settings are on the Parting Blow & Cycling section - the two used to be duplicated here.");
		if(configuration.RotationKeepCovered) {
			ImGui.Spacing();
			RotationLabel("Hold Snarl before a swap");
			int v=configuration.RotationCoveredHoldSeconds;
			if(ImGui.SliderInt("##bmh-covered-hold",ref v,0,30,"%ds")) {
				configuration.RotationCoveredHoldSeconds=v;
				configuration.Save();
			}
			SettingTip("Stop re-applying Snarl this long before the familiar cycle is due. Covered leaves with the familiar, so a cover spent seconds before Parting Blow is wasted and Snarl would then be on cooldown just as the replacement arrives.");
			RotationLabel("Release Covered at familiar HP");
			int v2=configuration.RotationCoveredReleaseHp;
			if(ImGui.SliderInt("##bmh-covered-release",ref v2,0,80,(v2==0)?"never":"%d%%")) {
				configuration.RotationCoveredReleaseHp=v2;
				configuration.Save();
			}
			SettingTip("Covered makes the familiar take every hit meant for you. When its HP falls to this, Challenge takes the enemy back onto you so the familiar survives its turn instead of dying and leaving its horn on cooldown. Skipped while you are under 50% HP yourself. 0 = never release.");
		}
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		RotationLabel("Battlehorn");
		int currentItem=Math.Clamp(configuration.RotationBattlehornSlot,0,3);
		if(ImGui.Combo("##bmh-battlehorn",ref currentItem,BattlehornLabels,BattlehornLabels.Length)) {
			configuration.RotationBattlehornSlot=currentItem;
			configuration.Save();
			if(currentItem>0) {
				rotationRunner.RequestSummon(currentItem);
			}
		}
		SettingTip("Which familiar to summon, or Auto to let the plugin choose.");
		if(configuration.RotationBattlehornSlot==0) {
			ImGui.Spacing();
			int v3=(int)configuration.BattlehornOrder;
			ImGui.TextDisabled("Auto picks by:");
			ImGui.SameLine();
			if(ImGui.RadioButton("Slot order (1-2-3)",ref v3,0)) {
				configuration.BattlehornOrder=BattlehornOrder.SlotOrder;
				configuration.Save();
			}
			SettingTip("Straight through the slots in order. Predictable, and what a three-familiar cycle should do.");
			ImGui.SameLine();
			if(ImGui.RadioButton("Kinship priority",ref v3,1)) {
				configuration.BattlehornOrder=BattlehornOrder.KinshipPriority;
				configuration.Save();
			}
			SettingTip("Pick the most useful kinship among the slots that are ready. Better single-familiar choice, but it will not follow slot order.");
			if(configuration.BattlehornOrder==BattlehornOrder.KinshipPriority) {
				DrawKinshipPriority();
			}
		}
	}
	private void DrawRotationAdvancedSection() {
		ImGui.TextDisabled("Axes and Trick");
		ImGui.Spacing();
		RotationLabel("Axe TP threshold");
		int v=configuration.RotationAxeTpThreshold;
		if(ImGui.SliderInt("##bmh-axe-tp",ref v,100,250)) {
			configuration.RotationAxeTpThreshold=v;
			configuration.Save();
		}
		SettingTip("Instinctual axes consume ALL your TP and scale from 400 potency at 100 TP to 1000 at 250. Firing one early throws the bar away, so the rotation holds until TP reaches this. Burn ignores it.");
		RotationLabel("Hold Trick until TP is at 100");
		if(ImGui.Checkbox("##bmh-trick-wait",ref configuration.RotationTrickWaitForTp)) {
			configuration.Save();
		}
		SettingTip("Trick's Heart lasts 7s and only pays off when an axe follows it inside that window, so Trick on an empty bar spends 100 Familiar TP for nothing.\n\nTrick first and the axe second is also what banks a Mastered Instinct - the yellow diamond Rally spends. Axe first only earns the blue one.");
		RotationLabel("Pool TP for the burst");
		if(ImGui.Checkbox("##bmh-pool-burst",ref configuration.RotationPoolForBurst)) {
			configuration.Save();
		}
		SettingTip("With no Heart held and the familiar's Trick still charging, don't spend the TP bar on an unpaired axe. It waits to follow Trick, so the combo banks a Mastered Instinct and builds toward the Rally finisher. Gives up after 25s, and never holds at 240+ TP. Burn ignores it.");
	}
	private void DrawKinshipPriority() {
		configuration.NormaliseKinshipPriority();
		ImGui.Spacing();
		ImGui.TextDisabled("Auto summons the highest-ranked kinship among your three slots:");
		Dictionary<BeastKin,int> dictionary=new Dictionary<BeastKin,int>();
		for(int i=0;i<3;i++) {
			BeastmasterCatalogEntry beastmasterCatalogEntry=BeastmasterTeam.SlotEntry(i);
			if(!(beastmasterCatalogEntry==null)) {
				BeastKin beastKin=BeastmasterKinship.KinOf(beastmasterCatalogEntry.Number);
				if(beastKin!=BeastKin.Unknown) {
					dictionary.TryAdd(beastKin,i+1);
				}
			}
		}
		List<int> kinshipPriority=configuration.KinshipPriority;
		int num=-1;
		int num2=-1;
		for(int j=0;j<kinshipPriority.Count;j++) {
			BeastKin beastKin2=(BeastKin)kinshipPriority[j];
			ImU8String strId=new ImU8String(3,1);
			strId.AppendLiteral("kin");
			strId.AppendFormatted(j);
			ImGui.PushID(strId);
			ImGui.BeginDisabled(j==0);
			if(ImGui.SmallButton("^")) {
				num=j;
				num2=j-1;
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			ImGui.BeginDisabled(j==kinshipPriority.Count-1);
			if(ImGui.SmallButton("v")) {
				num=j;
				num2=j+1;
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			bool flag=dictionary.TryGetValue(beastKin2,out var value);
			string text=(flag?$"{j+1}. {beastKin2}  (slot {value})":$"{j+1}. {beastKin2}");
			if(flag) {
				ImGui.TextColored(new Vector4(0.4f,0.9f,0.5f,1f),text);
			}
			else {
				ImGui.TextDisabled(text);
			}
			ImGui.SameLine();
			ImU8String text2=new ImU8String(2,1);
			text2.AppendLiteral("- ");
			text2.AppendFormatted(BeastmasterKinship.Effect(beastKin2));
			ImGui.TextDisabled(text2);
			ImGui.PopID();
		}
		if(num>=0&&num2>=0&&num2<kinshipPriority.Count) {
			List<int> list=kinshipPriority;
			int index=num;
			int index2=num2;
			int value2=kinshipPriority[num2];
			int value3=kinshipPriority[num];
			list[index]=value2;
			kinshipPriority[index2]=value3;
			configuration.Save();
		}
		if(dictionary.Count==0) {
			ImGui.TextColored(new Vector4(1f,0.8f,0.4f,1f),"None of your slots could be identified - Auto falls back to the first ready Battlehorn.");
		}
		ImGui.Spacing();
		if(ImGui.Checkbox("Prefer defensive kinship when hurt",ref configuration.KinshipMitigationWhenLow)) {
			configuration.Save();
		}
		SettingTip("Below the HP share set here, Beastkin, Vilekin and Scalekin outrank everything else in the list. Among those three your own order still decides.");
		if(ImGui.Checkbox("Also use defensive kinships on cooldown",ref configuration.KinshipMitigationOnCooldown)) {
			configuration.Save();
		}
		SettingTip("With Borrow, Beastskin, Vileskin and Scaleskin were only pressed below the HP share, so with Keep Covered on - the familiar taking the hits - they were made available and never used. This also presses them on cooldown while you are fighting.");
		if(configuration.KinshipMitigationWhenLow) {
			float v=configuration.KinshipMitigationHpThreshold;
			ImGui.SetNextItemWidth(200f);
			if(ImGui.SliderFloat("Defensive below HP",ref v,1f,100f,"%.0f%%")) {
				configuration.KinshipMitigationHpThreshold=Math.Clamp(v,1f,100f);
				configuration.Save();
			}
		}
		ImGui.Spacing();
	}
	private void DrawOverlaySettings() {
		ImGui.Text("Overlays");
		if(ImGui.Checkbox("Show overlay",ref configuration.ShowOverlay)) {
			configuration.Save();
		}
		SettingTip("The compact status window: state, familiar, gauges and the next action. Click its header to start/stop, right-click it to open this window.");
		if(ImGui.Checkbox("Lock overlay position",ref configuration.LockOverlay)) {
			configuration.Save();
		}
		if(ImGui.Checkbox("Auto-hide when idle",ref configuration.AutoHideOverlay)) {
			configuration.Save();
		}
		SettingTip("Hide the overlays when neither the rotation nor auto-capture is running.");
		if(ImGui.Checkbox("Show the rotation panels",ref configuration.ShowRotationOverlay)) {
			configuration.Save();
		}
		SettingTip("The floating rotation windows - switches, thresholds and the action list - so they can be flipped mid-pull without opening this window. Each panel below can also be shown or hidden on its own.");
		float v=configuration.OverlayBgAlpha;
		ImGui.SetNextItemWidth(200f);
		if(ImGui.SliderFloat("Overlay opacity",ref v,0f,1f,"%.2f")) {
			configuration.OverlayBgAlpha=v;
			configuration.Save();
		}
		float v2=configuration.OverlayWidth;
		ImGui.SetNextItemWidth(200f);
		if(ImGui.SliderFloat("Overlay width",ref v2,60f,1000f,"%.0f")) {
			configuration.OverlayWidth=v2;
			configuration.Save();
		}
		SettingTip("The overlay keeps this width and wraps long text inside it, rather than resizing itself every time the status text changes. Ctrl+click the slider to type any value.");
		ImGui.Spacing();
		ImGui.TextDisabled("The rotation windows (Rotation, Familiar, Actions) are set up in the Quick Toggles and Hotbar tabs.");
		ImGui.Spacing();
		ImGui.TextDisabled("Battlehorn slots (read from the client):");
		for(int i=0;i<3;i++) {
			ImU8String text=new ImU8String(14,3);
			text.AppendLiteral("   ");
			text.AppendFormatted(i+1);
			text.AppendLiteral(". ");
			text.AppendFormatted(BeastmasterTeam.SlotLabel(i));
			text.AppendLiteral("   (raw ");
			text.AppendFormatted(BeastmasterTeam.RawSlot(i));
			text.AppendLiteral(")");
			ImGui.TextDisabled(text);
		}
		int num=BeastmasterTeam.UnlockedCount();
		ImGui.TextDisabled((num>=0)?$"   Bestiary unlocked: {num} / 50":"   Bestiary unlocked: unreadable");
	}
	private static void SettingTip(string text) {
		if(ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize()*28f);
			ImGui.TextUnformatted(text);
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}
	}
	private void DrawRotationStuff() {
		ImGui.Text("Rotation Stuff");
		ImGui.TextDisabled("Everything about the AI rotation lives here now - live status and quick toggles on Main, every tuning knob on Settings.");
		ImGui.Separator();
		ImGui.Spacing();
		if(ImGui.BeginTabBar("##RotationStuffTabs")) {
			if(ImGui.BeginTabItem("Main##RotationStuffMain")) {
				ImGui.Spacing();
				DrawAutoOutput();
				ImGui.EndTabItem();
			}
			if(ImGui.BeginTabItem("Settings##RotationStuffSettings")) {
				ImGui.Spacing();
				DrawRotationStuffSettings();
				ImGui.EndTabItem();
			}
			ImGui.EndTabBar();
		}
	}
	private void DrawRotationStuffSettings() {
		ImGui.TextDisabled("Rotation behaviour, the quick toggles, the Actions hotbar and the overlay windows.");
		ImGui.Spacing();
		if(ImGui.BeginTabBar("##bmh-rotation-settings-tabs")) {
			if(ImGui.BeginTabItem("Options##bmhrs-options")) {
				DrawRotationSettings();
				ImGui.EndTabItem();
			}
			if(ImGui.BeginTabItem("Quick Toggles##bmhrs-toggles")) {
				DrawQuickToggleSettings();
				ImGui.EndTabItem();
			}
			if(ImGui.BeginTabItem("Hotbar##bmhrs-hotbar")) {
				DrawHotbarSettings();
				ImGui.EndTabItem();
			}
			if(ImGui.BeginTabItem("Overlay##bmhrs-overlay")) {
				DrawOverlaySettings();
				ImGui.EndTabItem();
			}
			ImGui.EndTabBar();
		}
	}
	private void DrawAutoOutput() {
		ImGui.TextDisabled("Configure auto-capture and auto-attack behaviour.");
		ImGui.Spacing();
		bool v=autoCaptureService.IsEnabled;
		ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(1f,0.82f,0.25f,1f));
		if(ImGui.Checkbox("Auto-attack",ref v)) {
			autoCaptureService.SetEnabled(v);
		}
		ImGui.PopStyleColor();
		ImGui.TextDisabled("Acts only on the hostile target you have selected. With no capture status applied it captures first, then runs the 1-2-3 combo.");
		DrawCaptureHpThreshold();
		DrawSettingCheckbox("Detailed mode","Show the detail lines in the overlay - reason, capture state, current familiar, gauge and advanced action candidates. Off by default.","ShowGaugeInOverlay",configuration.ShowGaugeInOverlay);
		ImGui.Spacing();
		ImGui.Text("Current mode");
		string text;
		if(autoCaptureService.IsEnabled) {
			text=(autoCaptureService.TryCapture?"Auto-capturing...":"Auto-attacking...");
		}
		else {
			text="Not enabled";
		}
		ImGui.Text(text);
		ImGui.TextDisabled("When enabled you can toggle \"Attempt capture\" from the overlay. Right-click the overlay to open settings.");
		ImGui.Spacing();
		DrawBeastmasterGauge();
		ImGui.Spacing();
		DrawBeastmasterGaugeGuide();
	}
	private string GetAdvancedActionModeText() {
		if(configuration.AdvancedActionsEnabled) {
			if(!configuration.BeastHeartCooperationEnabled) {
				if(!configuration.BeastSoulCooperationEnabled) {
					return "Enabled, but no cooperation mode is selected";
				}
				return "Beast Soul cooperation (blue)";
			}
			return "Beast Heart cooperation (yellow)";
		}
		return "Off - conserve resources";
	}
	private void DrawOverlayAdvancedActionToggles() {
		bool v=configuration.AdvancedActionsEnabled;
		if(ImGui.Checkbox("Advanced actions",ref v)) {
			configuration.AdvancedActionsEnabled=v;
			configuration.Save();
		}
		if(!configuration.AdvancedActionsEnabled) {
			return;
		}
		ImGui.Indent();
		bool v2=configuration.BeastHeartCooperationEnabled;
		if(ImGui.Checkbox("Beast Heart cooperation (yellow)",ref v2)) {
			configuration.BeastHeartCooperationEnabled=v2;
			if(v2) {
				configuration.BeastSoulCooperationEnabled=false;
			}
			configuration.Save();
		}
		bool v3=configuration.BeastSoulCooperationEnabled;
		if(ImGui.Checkbox("Beast Soul cooperation (blue)",ref v3)) {
			configuration.BeastSoulCooperationEnabled=v3;
			if(v3) {
				configuration.BeastHeartCooperationEnabled=false;
			}
			configuration.Save();
		}
		bool v4=configuration.AutoReleaseEnabled;
		if(ImGui.Checkbox("Release",ref v4)) {
			configuration.AutoReleaseEnabled=v4;
			configuration.Save();
		}
		ImGui.Unindent();
	}
	private void DrawCaptureHpThreshold() {
		float v=Math.Clamp(configuration.CaptureHpThreshold,1f,100f);
		ImGui.SetNextItemWidth(180f);
		if(ImGui.SliderFloat("Capture HP threshold",ref v,1f,100f,"%.0f%%")) {
			configuration.CaptureHpThreshold=v;
			configuration.Save();
		}
		ImGui.SameLine();
		ImGui.TextDisabled("Use the capture action when the target falls below this HP");
	}
	private void DrawBeastmasterGauge() {
		ImGui.Separator();
		ImGui.Text("Current gauge");
		ImGui.TextDisabled("Read-only view of the current Beastmaster gauge, sourced from JobGaugeManager.CurrentGauge.");
		BeastmasterGaugeSnapshot beastmasterGaugeSnapshot=gaugeSnapshot;
		ImGui.TextColored(beastmasterGaugeSnapshot.Available?new Vector4(0.35f,0.85f,0.55f,1f):new Vector4(0.9f,0.55f,0.35f,1f),beastmasterGaugeSnapshot.Status);
		if(!beastmasterGaugeSnapshot.Available) {
			return;
		}
		DrawCurrentSummon(beastmasterGaugeSnapshot);
		if(ImGui.BeginTable("BeastmasterGaugeState",2,ImGuiTableFlags.SizingStretchProp|ImGuiTableFlags.BordersInnerH)) {
			ImGui.TableSetupColumn("Field",ImGuiTableColumnFlags.WidthFixed,150f);
			ImGui.TableSetupColumn("Value",ImGuiTableColumnFlags.WidthStretch);
			DrawGaugeRow("Tenacity",$"{beastmasterGaugeSnapshot.Tp} / {250}","Base resources");
			DrawGaugeRow("Beast Power",$"{beastmasterGaugeSnapshot.BeastPower} / {250}","Beast Heart resources");
			byte whistleIndex=beastmasterGaugeSnapshot.WhistleIndex;
			DrawGaugeRow("Current whistle",(whistleIndex>=1&&whistleIndex<=3)?$"no. {beastmasterGaugeSnapshot.WhistleIndex}":"Not summoned","Current whistle type");
			DrawGaugeRow("Mastered Instinct",$"{beastmasterGaugeSnapshot.BeastHeartStacks} / 3 (Rally spends these)","Cooperation gauge");
			DrawGaugeRow("Natural Instinct",$"{beastmasterGaugeSnapshot.BeastSoulStacks} / 3 (Rallying Cheer spends these)","Cooperation gauge");
			DrawGaugeRow("Instinct byte",$"0x{beastmasterGaugeSnapshot.BeastHeart:X2} ({Convert.ToString(beastmasterGaugeSnapshot.BeastHeart,2).PadLeft(8,'0')})","Cooperation gauge");
			ImGui.EndTable();
		}
		if(!beastmasterGaugeSnapshot.StacksLookSane) {
			ImGui.TextColored(new Vector4(1f,0.5f,0.4f,1f),"Instinct byte is outside 0x00-0x0F, so it is not two 2-bit counters.");
			ImGui.TextColored(new Vector4(1f,0.8f,0.4f,1f),"The rotation has stopped gating Rally and Rallying Cheer on stacks and is firing them whenever the matching bar is low instead.");
		}
		ImU8String text=new ImU8String(18,1);
		text.AppendLiteral("Current decision: ");
		text.AppendFormatted(GetGaugeDecision(beastmasterGaugeSnapshot));
		ImGui.TextDisabled(text);
		ImU8String text2=new ImU8String(26,1);
		text2.AppendLiteral("Advanced action decision: ");
		text2.AppendFormatted(autoCaptureService.AdvancedActionStatus);
		ImGui.TextDisabled(text2);
		if(ImGui.CollapsingHeader("Duty actions##BeastmasterDutyRaw")) {
			List<uint> list=BeastmasterDutyActions.Offered();
			if(list.Count==0) {
				ImGui.TextDisabled("No duty actions in any slot - this duty offers none, or you are not in one.");
			}
			else {
				foreach(uint item in list) {
					DutyActionState dutyActionState=BeastmasterDutyActions.Read(item);
					ImU8String text3=new ImU8String(32,7);
					text3.AppendFormatted(item);
					text3.AppendLiteral(" ");
					text3.AppendFormatted(BeastmasterGameData.ActionName(item,"unknown"));
					text3.AppendLiteral(" - ");
					text3.AppendLiteral("usable ");
					text3.AppendFormatted(dutyActionState.Usable);
					text3.AppendLiteral(", charges ");
					text3.AppendFormatted(dutyActionState.Charges);
					text3.AppendLiteral("/");
					text3.AppendFormatted(dutyActionState.MaxCharges);
					text3.AppendLiteral(", ");
					text3.AppendLiteral("recast ");
					text3.AppendFormatted(dutyActionState.CooldownRemaining,"0.0");
					text3.AppendLiteral("/");
					text3.AppendFormatted(dutyActionState.CooldownTotal,"0.0");
					ImGui.TextUnformatted(text3);
				}
			}
			uint[] known=BeastmasterDutyActions.Known;
			for(int i=0;i<known.Length;i++) {
				uint actionId=known[i];
				DutyActionState dutyActionState2=BeastmasterDutyActions.Read(actionId);
				ImGui.TextDisabled(BeastmasterGameData.ActionName(actionId,actionId.ToString())+": "+(dutyActionState2.Present?"in a slot":"not offered here"));
			}
		}
		if(ImGui.CollapsingHeader("Raw gauge data##BeastmasterGaugeRaw")) {
			ImU8String text4=new ImU8String(11,1);
			text4.AppendLiteral("Address: 0x");
			text4.AppendFormatted(((IntPtr)beastmasterGaugeSnapshot.Address).ToInt64(),"X");
			ImGui.TextDisabled(text4);
			ImU8String text5=new ImU8String(11,1);
			text5.AppendLiteral("48 bytesï¼š");
			text5.AppendFormatted(beastmasterGaugeSnapshot.FormatRawBytes());
			ImGui.TextWrapped(text5);
			ImGui.TextDisabled("Field offsets: gauge +0x10, familiar Tenacity +0x11, current whistle +0x13, Beast Heart / Beast Soul +0x18.");
		}
	}
	private static void DrawCurrentSummon(BeastmasterGaugeSnapshot snapshot) {
		if(snapshot.SummonDataId==0) {
			ImGui.TextDisabled("Current familiar: none summoned");
			return;
		}
		BeastmasterCatalogEntry beastmasterCatalogEntry=BeastmasterCatalog.Entries.FirstOrDefault((BeastmasterCatalogEntry item) => item.Number==snapshot.SummonDataId-18915);
		if(beastmasterCatalogEntry==null) {
			ImU8String text=new ImU8String(28,2);
			text.AppendLiteral("Current familiar: ");
			text.AppendFormatted(snapshot.SummonName);
			text.AppendLiteral(" (DataId ");
			text.AppendFormatted(snapshot.SummonDataId);
			text.AppendLiteral(")");
			ImGui.TextDisabled(text);
			return;
		}
		ImGui.Text("Current familiar");
		ImGui.SameLine();
		ImGui.Text(beastmasterCatalogEntry.DisplayName);
		ImGui.SameLine();
		Vector4 col=GetAttributeColor(beastmasterCatalogEntry.Attribute);
		ImU8String text2=new ImU8String(2,1);
		text2.AppendLiteral("[");
		text2.AppendFormatted(beastmasterCatalogEntry.Attribute);
		text2.AppendLiteral("]");
		ImGui.TextColored(in col,text2);
		ImU8String text3=new ImU8String(22,2);
		text3.AppendLiteral("Ultimate: ");
		text3.AppendFormatted(GetActionName(beastmasterCatalogEntry.UltimateActionId));
		text3.AppendLiteral(" | Release: ");
		text3.AppendFormatted(GetActionName(beastmasterCatalogEntry.ReleaseActionId));
		ImGui.TextDisabled(text3);
	}
	private static string GetGaugeDecision(BeastmasterGaugeSnapshot snapshot) {
		if(snapshot.SummonDataId==0) {
			return "Waiting for a familiar";
		}
		if(snapshot.Tp<100) {
			return $"Building Tenacity ({snapshot.Tp}/{250})";
		}
		if(snapshot.BeastPower<100) {
			return $"Building Beast Power ({snapshot.BeastPower}/{250})";
		}
		if(snapshot.BeastHeartStacks<=0) {
			return "Tenacity and Beast Power are sufficient for the combo";
		}
		return "Tenacity and Beast Power are sufficient - pair with Beast Heart for a cooperation action";
	}
	private static void DrawGaugeRow(string name,object value,string description) {
		ImGui.TableNextRow();
		ImGui.TableNextColumn();
		ImGui.Text(name);
		ImGui.TableNextColumn();
		ImGui.Text(value.ToString());
		ImGui.SameLine();
		ImGui.TextDisabled(description);
	}
	private static void DrawBeastmasterGaugeGuide() {
		if(ImGui.CollapsingHeader("Gauge guide##BeastmasterGaugeGuide")) {
			ImGui.TextDisabled("The notes below summarise the Beastmaster job gauge and its action logic. The gauge panel is read-only and never writes to game memory.");
			ImGui.Separator();
			DrawGuideTitle("Tenacity / Beast Power");
			ImGui.TextWrapped("Once you learn the Beast Heart trait, the Beastmaster job gauge appears on your hotbar. Chaining successful actions raises Tenacity, and higher Tenacity means stronger weaponskills.");
			ImGui.TextWrapped("Tenacity and Beast Power are the two Beastmaster gauge resources, both capped at 250. The more you hold, the easier it is to meet the requirements of your actions and cooperation abilities.");
			ImGui.Spacing();
			DrawGuideTitle("Beast Heart");
			ImGui.TextWrapped("Once you learn Beast Heart II, the Beast Heart gauge is displayed. Landing a Beast Heart combo action grants one stack of Beast Heart.");
			ImGui.TextWrapped("Using Rouse consumes every stack of Beast Heart and raises Tenacity. The more stacks spent, the larger the gain.");
			ImGui.Spacing();
			DrawGuideTitle("Cooperation gauge");
			ImGui.TextWrapped("The cooperation gauge shows the state of your Beast Heart cooperation. There are four attributes - Winged, Fierce, Sturdy and Arcane - and each Beast Heart action lights its matching segment.");
			ImGui.TextWrapped("When either you or your familiar uses a Beast Heart action, the other side following up within 7 seconds triggers the cooperation follow-up damage.");
			ImGui.TextWrapped("The number beneath the cooperation gauge is the cooperation count. The higher it climbs, the stronger the cooperation action becomes.");
			ImGui.Spacing();
			DrawGuideTitle("Life / Death");
			ImGui.TextWrapped("Following the Winged -> Fierce -> Sturdy -> Arcane -> Winged cycle clockwise produces a stronger Life or Death cooperation action.");
			ImGui.TextWrapped("Triggering the second cooperation stage grants either the Life or Death Beast Heart attribute. It lights the centre of the cooperation gauge and shapes the follow-up.");
			ImGui.Spacing();
			ImGui.TextColored(new Vector4(1f,0.9f,0.25f,1f),"The full job gauge description is available at any time from the actions menu.");
		}
	}
	private static void DrawGuideTitle(string title) {
		ImGui.TextColored(new Vector4(1f,0.65f,0.2f,1f),title);
	}
	private static void DrawDependency(string name,bool available,string purpose) {
		ImGui.TextColored(available?new Vector4(0.35f,0.8f,0.48f,1f):new Vector4(0.9f,0.42f,0.38f,1f),available?"Available":"Not loaded");
		ImGui.SameLine();
		ImGui.Text(name);
		ImGui.SameLine();
		ImGui.TextDisabled(purpose);
	}
	private void DrawSettingCheckbox(string label,string description,string key,bool value) {
		ImU8String label2=new ImU8String(2,2);
		label2.AppendFormatted(label);
		label2.AppendLiteral("##");
		label2.AppendFormatted(key);
		if(ImGui.Checkbox(label2,ref value)) {
			switch(key) {
			case "HideCompletedQuests":
			configuration.HideCompletedQuests=value;
			break;
			case "AutoCompleteCatalogFromChat":
			configuration.AutoCompleteCatalogFromChat=value;
			break;
			case "UseFlightNavigation":
			configuration.UseFlightNavigation=value;
			break;
			case "SetFlagOnNavigation":
			configuration.SetFlagOnNavigation=value;
			break;
			case "ShowNavigationLogs":
			configuration.ShowNavigationLogs=value;
			break;
			case "AdvancedActionsEnabled":
			configuration.AdvancedActionsEnabled=value;
			break;
			case "BeastHeartCooperationEnabled":
			configuration.BeastHeartCooperationEnabled=value;
			if(value) {
				configuration.BeastSoulCooperationEnabled=false;
			}
			break;
			case "BeastSoulCooperationEnabled":
			configuration.BeastSoulCooperationEnabled=value;
			if(value) {
				configuration.BeastHeartCooperationEnabled=false;
			}
			break;
			case "AutoReleaseEnabled":
			configuration.AutoReleaseEnabled=value;
			break;
			case "ShowGaugeInOverlay":
			configuration.ShowGaugeInOverlay=value;
			break;
			}
			configuration.Save();
		}
		ImGui.TextDisabled(description);
	}
	private void DrawDebug() {
		ImGui.Text("DEBUG");
		ImGui.TextDisabled("Reads client names, stable identifiers and raw quest data.");
		ImGui.Separator();
		ImGui.SetNextItemWidth(-1f);
		ImGui.InputText("##DebugQuery",ref debugQuery,128);
		DrawCaptureDiagnostics();
		ImGui.Separator();
		DrawArenaSurvey();
		ImGui.Separator();
		DrawDebugSearchButtons();
		if(ImGui.Button("Read quest: The Beast Within")) {
			SetDebugResult(debugDataService.FindQuests("The Beast Within"));
		}
		ImGui.SameLine();
		if(ImGui.Button("Collect the state of every quest")) {
			SetDebugResult(questService.GetActiveQuestsDebug());
		}
		if(ImGui.Button("Read Beastmaster quest chain")) {
			SetDebugResult(debugDataService.FindBeastmasterQuestChain());
		}
		if(ImGui.Button("Read catalogue duty IDs")) {
			SetDebugResult(debugDataService.FindCatalogDuties());
		}
		ImGui.SameLine();
		if(ImGui.Button("Read auto-capture IDs")) {
			SetDebugResult(debugDataService.FindAutoCaptureData());
		}
		if(ImGui.Button("Read raw gauge data")) {
			SetDebugResult(debugDataService.GetBeastmasterGaugeRaw());
		}
		if(ImGui.Button("Read familiar attribute map")) {
			SetDebugResult(debugDataService.FindBeastmasterAttributes());
		}
		if(ImGui.Button("Read recommended gear item IDs")) {
			SetDebugResult(debugDataService.FindRecommendedEquipmentIds());
		}
		if(ImGui.Button("Read current target state")) {
			SetDebugResult(debugDataService.GetCurrentTargetDebug());
		}
		ImGui.SameLine();
		if(ImGui.Button("Read current combo state")) {
			SetDebugResult(debugDataService.GetComboDebug());
		}
		if(ImGui.Button("Read cooperation verification data")) {
			SetDebugResult(debugDataService.GetCooperationValidationDebug());
		}
		if(ImGui.Button("Read current character")) {
			SetDebugResult(debugDataService.GetCharacter());
		}
		ImGui.SameLine();
		if(ImGui.Button("Read current position")) {
			SetDebugResult(debugDataService.GetLocation());
		}
		ImGui.SameLine();
		if(ImGui.Button("Copy result")) {
			ImGui.SetClipboardText(debugResult);
		}
		ImGui.Separator();
		if(ImGui.BeginChild("DebugResult",Vector2.Zero,border: true)) {
			ImGui.TextUnformatted(debugResult);
		}
		ImGui.EndChild();
	}
	private void DrawDebugSearchButtons() {
		if(ImGui.Button("Read jobs")) {
			SetDebugResult(debugDataService.FindClassJobs(debugQuery));
		}
		ImGui.SameLine();
		if(ImGui.Button("Read quests")) {
			SetDebugResult(debugDataService.FindQuests(debugQuery));
		}
		ImGui.SameLine();
		if(ImGui.Button("Read items")) {
			SetDebugResult(debugDataService.FindItems(debugQuery));
		}
		ImGui.SameLine();
		if(ImGui.Button("Read NPCs")) {
			SetDebugResult(debugDataService.FindNpcs(debugQuery));
		}
		ImGui.SameLine();
		if(ImGui.Button("Read monsters")) {
			SetDebugResult(debugDataService.FindMonsters(debugQuery));
		}
		ImGui.SameLine();
		if(ImGui.Button("Read duties")) {
			SetDebugResult(debugDataService.FindDuties(debugQuery));
		}
	}
	private void SetDebugResult(string result) {
		debugResult=result;
		ImGui.SetClipboardText(result);
	}
	private bool OverlayShouldDraw() {
		if(!DalamudApi.ClientState.IsLoggedIn) {
			return false;
		}
		if(DalamudApi.ObjectTable.LocalPlayer==null) {
			return false;
		}
		if(DalamudApi.PlayerState.ClassJob.RowId!=43) {
			return false;
		}
		if(configuration.AutoHideOverlay&&!configuration.RotationEnabled&&!autoCaptureService.IsEnabled&&!crucibleService.IsRunning&&!CrucibleEntryRules.AtLauda()) {
			return false;
		}
		return true;
	}
	private void DrawOverlays() {
		if(configuration.ShowOverlay&&OverlayShouldDraw()) {
			DrawMainOverlay();
		}
		if(configuration.ShowRotationOverlay&&OverlayShouldDraw()) {
			DrawRotationToggleOverlay();
		}
	}
	private void DrawMainOverlay() {
		ImGuiWindowFlags imGuiWindowFlags=ImGuiWindowFlags.NoNav|ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoResize|ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoScrollWithMouse|ImGuiWindowFlags.NoFocusOnAppearing;
		if(configuration.LockOverlay) {
			imGuiWindowFlags|=ImGuiWindowFlags.NoMove;
		}
		ImGui.SetNextWindowPos(new Vector2(20f,180f),ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowBgAlpha(configuration.OverlayBgAlpha);
		ImGui.SetNextWindowSize(new Vector2(Math.Max(40f,configuration.OverlayWidth),0f),ImGuiCond.Always);
		if(!ImGui.Begin("##BmhOverlay",imGuiWindowFlags)) {
			ImGui.End();
			return;
		}
		DrawOverlayHeader();
		if(ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)&&ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
			configuration.SelectedMainSection="settings";
			configuration.Save();
			isMainWindowOpen=true;
		}
		if(overlayCollapsed) {
			ImGui.End();
			return;
		}
		ImGui.Separator();
		ImGui.PushTextWrapPos(0f);
		bool flag=CrucibleContextVisible();
		if(ImGui.BeginTabBar("##bmh-overlay-tabs")) {
			if(ImGui.BeginTabItem("Combat")) {
				DrawOverlayCombatCockpit();
				ImGui.EndTabItem();
			}
			if(ImGui.BeginTabItem("Capture")) {
				DrawOverlayCapture();
				ImGui.EndTabItem();
			}
			if(flag&&ImGui.BeginTabItem("Crucible")) {
				DrawOverlayCrucible(debug: false);
				ImGui.EndTabItem();
			}
			if(ImGui.BeginTabItem("Debug")) {
				if(flag) {
					DrawOverlayCrucible(debug: true);
					ImGui.Separator();
				}
				DrawOverlayUniversalityDebug();
				ImGui.EndTabItem();
			}
			ImGui.EndTabBar();
		}
		ImGui.PopTextWrapPos();
		ImGui.End();
	}
	private bool CrucibleContextVisible() {
		return true;
	}
	private void DrawOverlayCapture() {
		List<BeastmasterCatalogEntry> list=(from entry in BeastmasterCatalog.Entries
			where entry.Number>1&&BeastmasterTeam.IsUnlocked(entry.Number)!=true
			orderby (entry.LocationType==BeastmasterCatalogLocationType.Duty)?1:0,BeastmasterCaptureTargeting.LowestLevel(entry.Level) ?? int.MaxValue,entry.Number
			select entry).ToList();
		if(!BeastmasterTeam.IsUnlocked(1).HasValue) {
			ImGui.TextColored(new Vector4(1f,0.8f,0.4f,1f),"Bestiary unreadable - showing every beast.");
		}
		ImGui.TextDisabled((list.Count==0)?"Every beast is in your Bestiary.":$"{list.Count} still to capture");
		if(ImGui.BeginChild("##bmh-capture-list",new Vector2(0f,220f),border: true)) {
			foreach(BeastmasterCatalogEntry item in list) {
				ImGui.PushID(item.Number);
				ImGui.TextUnformatted(item.DisplayName);
				ImGui.SameLine();
				ImU8String text=new ImU8String(3,1);
				text.AppendLiteral("Lv ");
				text.AppendFormatted(item.Level);
				ImGui.TextDisabled(text);
				if(item.LocationType==BeastmasterCatalogLocationType.Duty) {
					ImGui.PushTextWrapPos(0f);
					ImGui.TextDisabled(item.DisplayLocation);
					ImGui.PopTextWrapPos();
					ImGui.Spacing();
				}
				else if(item.MapX.HasValue&&item.MapY.HasValue) {
					bool flag=BeastmasterCaptureTargeting.NeedsHigherLevel(item,PlayerLevel());
					ImGui.SameLine(ImGui.GetWindowContentRegionMax().X-(flag?100f:62f));
					ImGui.BeginDisabled(flag);
					if(ImGui.SmallButton(flag?"Need Higher Lv":"Navigate")) {
						navigationService.Navigate(item);
					}
					ImGui.EndDisabled();
					if(flag&&ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
						ImU8String tooltip=new ImU8String(43,2);
						tooltip.AppendLiteral("Its level range starts at ");
						tooltip.AppendFormatted(BeastmasterCaptureTargeting.LowestLevel(item.Level));
						tooltip.AppendLiteral("; you are level ");
						tooltip.AppendFormatted(PlayerLevel());
						tooltip.AppendLiteral(".");
						ImGui.SetTooltip(tooltip);
					}
				}
				ImGui.PopID();
			}
		}
		ImGui.EndChild();
	}
	private void DrawOverlayCombatCockpit() {
		ImGui.TextColored(in OverlayGold,"COMBAT");
		DrawOverlayFamiliarRow();
		DrawOverlayGauges();
		DrawOverlayWheel();
		DrawOverlayNextAction();
		DrawOverlayTargetLine();
	}
	private void DrawOverlayUniversalityDebug() {
		ImGui.Spacing();
		if(ImGui.CollapsingHeader("Infinitive debug##bmh-inf-debug",ImGuiTreeNodeFlags.DefaultOpen)) {
			byte b=DalamudApi.ObjectTable.LocalPlayer?.Level ?? 0;
			var (text,flag,array)=UniversalityDebug.Summarise(rotationRunner,configuration,b);
			ImGui.TextColored(flag?OverlayGreen:OverlayGold,text);
			string[] array2=array;
			foreach(string text2 in array2) {
				ImGui.TextColored(in OverlayGrey,text2);
			}
			if(ImGui.SmallButton("Copy Infinitive debug##bmh-inf-copy")) {
				ImGui.SetClipboardText($"BMH Infinitive debug (v{typeof(BeastmasterPlugin).Assembly.GetName().Version}, level {b})"+Environment.NewLine+text+Environment.NewLine+string.Join(Environment.NewLine,array));
			}
			Tip("Copies this summary as text. It contains no character name, world or IDs.");
		}
	}
	private void DrawOverlayCrucible(bool debug) {
		bool flag=DalamudApi.Condition[ConditionFlag.BoundByDuty];
		if(debug) {
			DrawOverlayCrucibleDebug(flag);
			return;
		}
		float x=ImGui.GetContentRegionAvail().X;
		ImGui.BeginDisabled(crucibleService.IsRunning|flag);
		int currentItem=Math.Clamp(configuration.CrucibleBoard,0,BeastmasterCrucibleService.BoardNames.Length-1);
		ImGui.SetNextItemWidth(x);
		if(ImGui.Combo("##bmh-overlay-board",ref currentItem,"First Board\0Second Board\0Third Board\0First Master's Board\0Second Master's Board\0")) {
			configuration.CrucibleBoard=Math.Clamp(currentItem,0,BeastmasterCrucibleService.BoardNames.Length-1);
			configuration.Save();
		}
		Tip(BeastmasterCrucibleService.BoardNames[Math.Clamp(configuration.CrucibleBoard,0,BeastmasterCrucibleService.BoardNames.Length-1)]);
		ImGui.EndDisabled();
		ImGui.BeginDisabled(crucibleService.IsRunning|flag);
		int currentItem2=Math.Clamp(configuration.CrucibleDifficulty,0,3);
		ImGui.SetNextItemWidth(x);
		if(ImGui.Combo("##bmh-overlay-difficulty",ref currentItem2,"Standard\0First Degree\0Second Degree\0Third Degree\0")) {
			configuration.CrucibleDifficulty=Math.Clamp(currentItem2,0,3);
			configuration.Save();
		}
		ImGui.EndDisabled();
		Tip("Difficulty for the board. Only applied when the Difficulty selector is on Board Layout (it needs degrees unlocked); ignored otherwise.");
		bool v=configuration.CrucibleLoopSelectedBoard;
		if(ImGui.Checkbox("Loop Selected Board",ref v)) {
			configuration.CrucibleLoopSelectedBoard=v;
			configuration.Save();
		}
		Tip("After a completed run, return to Lauda and enter this board again.");
		ImGui.Spacing();
		ImGui.TextColored(in OverlayGold,"MODES");
		ImGui.Separator();
		bool v2=configuration.CrucibleRecommendedTeam;
		if(ImGui.Checkbox("Recommended Team Mode",ref v2)) {
			SelectCrucibleMode(v2?CrucibleMode.Recommended:CrucibleMode.None);
		}
		Tip("Let the game pick the familiars. With this off you choose them yourself in the Team Composition window and press Team ready.");
		bool v3=configuration.CrucibleLevelUpBeasts;
		if(ImGui.Checkbox("Level Up Familiar(s) Mode",ref v3)) {
			SelectCrucibleMode(v3?CrucibleMode.LevelUp:CrucibleMode.None);
		}
		Tip("Field the lowest-ranked familiars so they gain rank, instead of the meta team. Each full read scans every familiar's rank and exp, unregisters the old team and registers the lowest-ranked ones that can still gain, up to the board's team size. Replaces Recommended Team Mode.");
		bool v4=configuration.CrucibleSuggestedTeamMode;
		if(ImGui.Checkbox("Suggested Team Mode (WIP)",ref v4)) {
			SelectCrucibleMode(v4?CrucibleMode.Suggested:CrucibleMode.None);
			if(v4&&!ReactionHelperLoaded()) {
				ImGui.OpenPopup("##suggestedteammodenorh");
			}
		}
		Tip("Follows the guide's team, targeting and familiar usage per fight (Crucible Board(s) Options below). Still a work in progress - the guide does not yet cover every fight.");
		DrawReactionHelperWarning();
		ImGui.Spacing();
		ImGui.Separator();
		DrawCrucibleChores(x);
		ImGui.Spacing();
		if(crucibleService.IsRunning) {
			if(ImGui.Button("Stop",new Vector2(x,0f))) {
				crucibleService.Stop();
			}
		}
		else if(!flag&&ImGui.Button("Start",new Vector2(x,0f))) {
			crucibleService.Start(Math.Clamp(configuration.CrucibleBoard,0,BeastmasterCrucibleService.BoardNames.Length-1),configuration.CrucibleRecommendedTeam);
		}
		if(flag&&configuration.CrucibleBoardAssist) {
			bool paused=crucibleBoardService.Paused;
			if(paused) {
				ImGui.PushStyleColor(ImGuiCol.Text,OverlayGreen);
			}
			if(ImGui.Button(paused?"Resume routing":"Pause routing",new Vector2(x,0f))) {
				crucibleBoardService.TogglePause();
			}
			if(paused) {
				ImGui.PopStyleColor();
			}
			Tip(paused?"Routing is held. Loot, treasure, the shop and fight items still run - only the walk to the next platform is stopped.":"Stop walking to the next platform, so you can move yourself. Everything else carries on.");
		}
		ImGui.Separator();
		if(ImGui.Button("Copy Crucible Diagnostics",new Vector2(x,0f))) {
			ImGui.SetClipboardText(CrucibleDiagnosticsReport());
		}
		Tip("Copies a short report for troubleshooting Crucible automation. Contains no character name, world, IDs or positions.");
	}
	private void DrawOverlayCrucibleDebug(bool inDuty) {
		string text=(inDuty?crucibleBoardService.Status:crucibleService.Status);
		if(!string.IsNullOrWhiteSpace(text)) {
			ImGui.PushStyleColor(ImGuiCol.Text,ImGui.GetStyle().Colors[1]);
			ImGui.TextWrapped(text);
			ImGui.PopStyleColor();
		}
		ImGui.TextDisabled(crucibleBoardService.TokenStatus);
		DrawOverlayHealthBars();
		DrawOverlayCombatState();
	}
	private void DrawOverlayHealthBars() {
		IPlayerCharacter player=DalamudApi.ObjectTable.LocalPlayer;
		if(player!=null) {
			ImGui.Separator();
			ImGui.TextColored(in OverlayGold,"HEALTH");
			DrawOverlayGaugeBar("You",(int)player.CurrentHp,(int)Math.Max(1u,player.MaxHp),new Vector4(0.45f,0.85f,0.45f,1f));
			IBattleNpc battleNpc=DalamudApi.ObjectTable.OfType<IBattleNpc>().FirstOrDefault((IBattleNpc npc) => npc.OwnerId==player.GameObjectId&&npc.MaxHp!=0);
			if(battleNpc==null) {
				ImGui.TextColored(in OverlayGrey,"No familiar out");
			}
			else {
				DrawOverlayGaugeBar(battleNpc.Name.TextValue,(int)battleNpc.CurrentHp,(int)Math.Max(1u,battleNpc.MaxHp),new Vector4(0.4f,0.7f,1f,1f));
			}
		}
	}
	private void DrawOverlayCombatState() {
		if(!rotationRunner.OutOfRange&&!rotationRunner.BossModPresent&&!BeastmasterDutyActions.Available) {
			return;
		}
		ImGui.Separator();
		ImGui.TextColored(in OverlayGold,"STATE");
		if(rotationRunner.OutOfRange) {
			ImGui.TextColored(in OverlayGrey,"Range");
			ImGui.SameLine();
			ImGui.TextColored(in OverlayGrey,rotationRunner.StatusText);
		}
		if(rotationRunner.BossModPresent) {
			string bossModEncounter=rotationRunner.BossModEncounter;
			ImGui.TextColored(in OverlayGrey,"BossMod");
			ImGui.SameLine();
			Vector4 col=rotationRunner.BossModDodging?OverlayGrey:OverlayGreen;
			string? text;
			if(bossModEncounter.Length>0) {
				text=bossModEncounter+(rotationRunner.BossModDodging?" - repositioning":string.Empty);
			}
			else {
				text=(rotationRunner.BossModDodging?"repositioning":"no module active");
			}
			ImGui.TextColored(in col,text);
			if(configuration.ShieldChargeGapClose||configuration.ShieldChargeAoe) {
				string gapCloseStatus=rotationRunner.GapCloseStatus;
				ImGui.TextColored(in OverlayGrey,"Gap close");
				ImGui.SameLine();
				ImGui.TextColored(gapCloseStatus.StartsWith("Charging",StringComparison.Ordinal)?OverlayGreen:OverlayGrey,gapCloseStatus);
			}
		}
		if(BeastmasterDutyActions.Available) {
			string coveredStatus=rotationRunner.CoveredStatus;
			ImGui.TextColored(in OverlayGrey,configuration.RotationKeepCovered?"Covered":"Challenge");
			ImGui.SameLine();
			ImGui.TextColored((coveredStatus.StartsWith("Covered is up",StringComparison.Ordinal)||coveredStatus.StartsWith("Snarling",StringComparison.Ordinal)||coveredStatus.StartsWith("Challenging",StringComparison.Ordinal))?OverlayGreen:OverlayGrey,coveredStatus);
		}
	}
	private void DrawOverlayHeader() {
		bool rotationEnabled=configuration.RotationEnabled;
		Vector4 col=(rotationEnabled?OverlayGreen:OverlayGrey);
		string value=(rotationEnabled?("Rotation: "+rotationRunner.Phase):"Rotation: Off");
		ImGui.PushStyleColor(ImGuiCol.Text,col);
		ImU8String label=new ImU8String(22,1);
		label.AppendFormatted(value);
		label.AppendLiteral("###bmh-rotation-toggle");
		if(ImGui.Selectable(label)) {
			rotationRunner.SetEnabled(!rotationEnabled);
		}
		ImGui.PopStyleColor();
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Click to start/stop the rotation.\nRight-click the overlay to open settings.\nCtrl-click to collapse.");
		}
		if(ImGui.IsItemClicked(ImGuiMouseButton.Left)&&ImGui.GetIO().KeyCtrl) {
			overlayCollapsed=!overlayCollapsed;
		}
		if(configuration.RotationBurn) {
			ImGui.SameLine();
			ImGui.TextColored(in OverlayRed,"[BURN]");
		}
		if(configuration.RotationUseAoe&&rotationRunner.EnemiesNearTarget>=configuration.RotationAoeThreshold) {
			ImGui.SameLine();
			ImU8String text=new ImU8String(6,1);
			text.AppendLiteral("[AoE ");
			text.AppendFormatted(rotationRunner.EnemiesNearTarget);
			text.AppendLiteral("]");
			ImGui.TextColored(in OverlayGold,text);
		}
	}
	private void DrawOverlayFamiliarRow() {
		BeastmasterGaugeSnapshot gauge=BeastmasterGaugeSnapshot.Read();
		BeastmasterCatalogEntry beastmasterCatalogEntry=((gauge.SummonDataId==0)?null:BeastmasterCatalog.Entries.FirstOrDefault((BeastmasterCatalogEntry e) => e.SummonDataId==gauge.SummonDataId));
		ImGui.TextColored(in OverlayGrey,"Familiar");
		ImGui.SameLine();
		if(beastmasterCatalogEntry!=null) {
			ImGui.TextColored(in OverlayBlue,beastmasterCatalogEntry.DisplayName);
		}
		else if(gauge.SummonDataId!=0) {
			ImU8String text=new ImU8String(7,1);
			text.AppendLiteral("DataId ");
			text.AppendFormatted(gauge.SummonDataId);
			ImGui.TextColored(in OverlayBlue,text);
		}
		else {
			ImGui.TextColored(in OverlayGrey,"none summoned");
		}
		int num=Math.Clamp(configuration.RotationBattlehornSlot,0,3);
		byte playerLevel=DalamudApi.ObjectTable.LocalPlayer?.Level ?? 1;
		string text2=((num==0)?BattlehornLabels[0]:$"{num}. {BeastmasterTeam.SlotLabel(num-1)}");
		ImGui.SetNextItemWidth(-1f);
		if(ImGui.BeginCombo("##bmh-horn",text2)) {
			for(int num2=0;num2<BattlehornLabels.Length;num2++) {
				if(num2!=0&&!BeastmasterActions.IsUnlocked(BeastmasterActions.Battlehorns[num2-1],playerLevel)) {
					int value=BeastmasterActions.UnlockLevel(BeastmasterActions.Battlehorns[num2-1]);
					ImU8String text3=new ImU8String(9,2);
					text3.AppendFormatted(BattlehornLabels[num2]);
					text3.AppendLiteral(" (level ");
					text3.AppendFormatted(value);
					text3.AppendLiteral(")");
					ImGui.TextDisabled(text3);
					continue;
				}
				string text4;
				if(num2==0) {
					text4=BattlehornLabels[0];
				}
				else {
					BeastmasterCatalogEntry beastmasterCatalogEntry2=BeastmasterTeam.SlotEntry(num2-1);
					BeastKin beastKin=((!(beastmasterCatalogEntry2==null))?BeastmasterKinship.KinOf(beastmasterCatalogEntry2.Number):BeastKin.Unknown);
					text4=((beastKin==BeastKin.Unknown)?$"{num2}. {BeastmasterTeam.SlotLabel(num2-1)}":$"{num2}. {BeastmasterTeam.SlotLabel(num2-1)} [{beastKin}]");
				}
				if(ImGui.Selectable(text4,num==num2)) {
					configuration.RotationBattlehornSlot=num2;
					configuration.Save();
					if(num2>0) {
						rotationRunner.RequestSummon(num2);
					}
				}
			}
			ImGui.EndCombo();
		}
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Which familiar the rotation summons.");
		}
		if(num==0) {
			string value2=((configuration.BattlehornOrder==BattlehornOrder.KinshipPriority)?"by kinship":"slot order 1-2-3");
			ImU8String text5=new ImU8String(6,1);
			text5.AppendLiteral("Auto: ");
			text5.AppendFormatted(value2);
			ImGui.TextColored(in OverlayGrey,text5);
			Tip("Change this with Kin order on the Familiar panel.");
		}
	}
	private void DrawOverlayGauges() {
		BeastmasterGaugeSnapshot beastmasterGaugeSnapshot=BeastmasterGaugeSnapshot.Read();
		if(!beastmasterGaugeSnapshot.Available) {
			ImGui.TextColored(in OverlayGrey,beastmasterGaugeSnapshot.Status);
			return;
		}
		DrawOverlayGaugeBar("Tenacity",(int)beastmasterGaugeSnapshot.Tp,250f,new Vector4(0.95f,0.75f,0.2f,1f));
		DrawOverlayGaugeBar("Beast Power",(int)beastmasterGaugeSnapshot.BeastPower,250f,new Vector4(0.35f,0.7f,1f,1f));
		if(beastmasterGaugeSnapshot.BeastHeartStacks>0||beastmasterGaugeSnapshot.BeastSoulStacks>0) {
			ImU8String text=new ImU8String(20,2);
			text.AppendLiteral("Mastered ");
			text.AppendFormatted(beastmasterGaugeSnapshot.BeastHeartStacks);
			text.AppendLiteral("   Natural ");
			text.AppendFormatted(beastmasterGaugeSnapshot.BeastSoulStacks);
			ImGui.TextColored(in OverlayGrey,text);
		}
	}
	private void DrawOverlayWheel() {
		InstinctAffinity heldHeart=rotationRunner.HeldHeart;
		InstinctAffinity wantedAffinity=rotationRunner.WantedAffinity;
		ImGui.TextColored(in OverlayGrey,"Wheel");
		ImGui.SameLine();
		if(heldHeart==InstinctAffinity.None) {
			ImGui.TextColored(in OverlayGrey,"no Heart held");
		}
		else {
			ImGui.TextColored(in OverlayBlue,heldHeart.ToString());
			ImGui.SameLine();
			ImGui.TextColored(in OverlayGrey,"->");
			ImGui.SameLine();
			ImGui.TextColored(in OverlayGold,wantedAffinity.ToString());
		}
		DrawOverlayInfinitive();
		DrawOverlayKinship();
		DrawOverlayCycle();
	}
	private void DrawOverlayKinship() {
		BeastKin borrowedKinship=rotationRunner.BorrowedKinship;
		if(borrowedKinship!=BeastKin.Unknown) {
			ImGui.TextColored(in OverlayGrey,"Beast Mode");
			ImGui.SameLine();
			ImGui.TextColored(in OverlayGold,borrowedKinship.ToString());
			Tip(BeastmasterKinship.Effect(borrowedKinship));
		}
	}
	private void DrawOverlayCycle() {
		if(configuration.RotationCycleFamiliars&&configuration.RotationEnabled) {
			ImGui.TextColored(in OverlayGrey,"Cycle");
			ImGui.SameLine();
			string cycleStatus=rotationRunner.CycleStatus;
			bool flag=cycleStatus.StartsWith("Cycling now",StringComparison.Ordinal);
			ImGui.PushStyleColor(ImGuiCol.Text,flag?OverlayGold:ImGui.GetStyle().Colors[1]);
			ImGui.TextUnformatted(cycleStatus);
			ImGui.PopStyleColor();
			Tip(rotationRunner.CycleDetail);
		}
	}
	private void DrawOverlayInfinitive() {
		uint heldInfinitiveHalf=rotationRunner.HeldInfinitiveHalf;
		string text;
		switch(heldInfinitiveHalf) {
		case 0u:
		return;
		default:
		text="Moonstalker";
		break;
		case 4599u:
		text="Sunstrider";
		break;
		}
		string text2=text;
		string value=((heldInfinitiveHalf==4599)?"Moonstalker":"Sunstrider");
		ImGui.TextColored(in OverlayGrey,"Infinitive");
		ImGui.SameLine();
		ImGui.TextColored(in OverlayBlue,text2);
		ImGui.SameLine();
		if(rotationRunner.InfinitiveReady) {
			ImGui.TextColored(in OverlayGold,"- next combo completes it");
			return;
		}
		ImU8String text3=new ImU8String(8,1);
		text3.AppendLiteral("- needs ");
		text3.AppendFormatted(value);
		ImGui.TextColored(in OverlayGrey,text3);
	}
	private void DrawOverlayNextAction() {
		ImGui.Separator();
		ImGui.TextColored(in OverlayGrey,"Next");
		ImGui.SameLine();
		ImGui.TextColored(configuration.RotationEnabled?OverlayGold:OverlayGrey,rotationRunner.NextActionName);
		if(configuration.ShowGaugeInOverlay&&!string.IsNullOrWhiteSpace(rotationRunner.NextActionReason)) {
			ImGui.PushStyleColor(ImGuiCol.Text,ImGui.GetStyle().Colors[1]);
			ImGui.TextWrapped(rotationRunner.NextActionReason);
			ImGui.PopStyleColor();
		}
	}
	private void DrawOverlayTargetLine() {
		IGameObject target=DalamudApi.TargetManager.Target;
		ImGui.TextColored(in OverlayGrey,"Target");
		ImGui.SameLine();
		if(target==null) {
			ImGui.TextColored(in OverlayGrey,"No Target");
		}
		else {
			ImGui.TextColored(new Vector4(0.9f,0.9f,0.9f,1f),target.Name.TextValue);
		}
	}
	private static void Tip(string tooltip) {
		if(ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize()*26f);
			ImGui.TextUnformatted(tooltip);
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}
	}
	private List<QuickToggle> BuildQuickToggles() {
		BeastmasterConfiguration c=configuration;
		int num=25;
		List<QuickToggle> list=new List<QuickToggle>(num);
		CollectionsMarshal.SetCount(list,num);
		Span<QuickToggle> span=CollectionsMarshal.AsSpan(list);
		span[0]=T("AiAssist","AI Assist","rotation",() => RotationToggleOverrides.Get("AiAssist",c.RotationEnabled),() => "Let the plugin play Beastmaster for you: it presses the next rotation action on your current target. Everything else in this window shapes what it does.",() => {
			SetAiAssist(!c.RotationEnabled);
		});
		span[1]=T("Capture","Capture","rotation",() => RotationToggleOverrides.Get("Capture",autoCaptureService.TryCapture),() => "Use Capture on beasts you have not befriended yet, once they are below the HP threshold. Targets the game says cannot be captured are attacked instead of being pointlessly retried.",() => {
			autoCaptureService.SetTryCapture(!autoCaptureService.TryCapture);
		});
		span[2]=T("Aoe","AoE","rotation",() => RotationToggleOverrides.Get("Aoe",c.RotationUseAoe),() => $"Use area actions once enough enemies are clustered on the target. Currently {rotationRunner.EnemiesNearTarget} near it.",() => {
			Flip(ref c.RotationUseAoe);
		});
		span[3]=T("Opener","Opener","rotation",() => RotationToggleOverrides.Get("Opener",c.RotationUseOpener),() => "Run the opener from a standing start instead of dropping straight into filler.",() => {
			Flip(ref c.RotationUseOpener);
		});
		span[4]=T("Cooldowns","Cooldowns","rotation",() => RotationToggleOverrides.Get("Cooldowns",c.RotationUseCooldowns),() => "Allow Rally, Rallying Cheer, Shield Charge and One with Nature.",() => {
			Flip(ref c.RotationUseCooldowns);
		});
		span[5]=T("Rally","Rally","rotation",() => RotationToggleOverrides.Get("Rally",c.RotationUseRally),() => "Allow Rally to spend Mastered Instinct. Cooldowns must also be on. Reaction Helper may temporarily hold this.",() => {
			Flip(ref c.RotationUseRally);
		});
		span[6]=T("RallyingCheer","Rallying Cheer","rotation",() => RotationToggleOverrides.Get("RallyingCheer",c.RotationUseRallyingCheer),() => "Allow Rallying Cheer to spend Natural Instinct. Cooldowns must also be on. Reaction Helper may temporarily hold this.",() => {
			Flip(ref c.RotationUseRallyingCheer);
		});
		span[7]=T("PowerActions","Power actions","rotation",() => RotationToggleOverrides.Get("PowerActions",c.RotationUsePowerActions),() => "Allow Trick and TP-spending axes. When off, normal filler continues and resources are saved.",() => {
			Flip(ref c.RotationUsePowerActions);
		});
		span[8]=T("Burn","Burn","rotation",() => RotationToggleOverrides.Get("Burn",c.RotationBurn),() => "Spend everything as it comes up instead of holding resources for the wheel. Lower potency overall - for a dying boss or the end of a pull.",() => {
			Flip(ref c.RotationBurn);
		});
		span[9]=T("AutoAttack","Auto-attack","rotation",() => RotationToggleOverrides.Get("AutoAttack",c.RotationAutoAttack),() => "On: the game's auto-attack runs as normal. Off: Beast Master Helper switches auto-attack off whenever it is running (the game starts it again on the next action, so this is checked every frame). Reaction Helper can switch this.",() => {
			Flip(ref c.RotationAutoAttack);
		});
		span[10]=T("GapClose","Gap close","rotation",() => RotationToggleOverrides.Get("GapClose",c.ShieldChargeGapClose),() => "Use Shield Charge to close on a target out of melee range. Reaction Helper may temporarily hold this.",() => {
			Flip(ref c.ShieldChargeGapClose);
		},() => c.RotationUseCooldowns);
		span[11]=T("ChargeAoe","Charge AoE","rotation",() => RotationToggleOverrides.Get("ChargeAoe",c.ShieldChargeAoe),() => $"Also fire Shield Charge on cooldown when {c.ShieldChargeAoeCount}+ enemies are within {Math.Max(c.ShieldChargeAoeRadius,6f):0}y of the enemy it would land on (the charge hits around your target, not around you). Off by default - burning the charge means walking to the next target.",() => {
			Flip(ref c.ShieldChargeAoe);
		},() => c.RotationUseCooldowns);
		span[12]=T("TrickWaitsTp","Trick waits TP","rotation",() => RotationToggleOverrides.Get("TrickWaitsTp",c.RotationTrickWaitForTp),() => "Hold Trick until your own TP is also at 100. Its Heart lasts 7s and only pays off when an axe follows it, so Trick on an empty bar spends 100 Familiar TP for nothing.",() => {
			Flip(ref c.RotationTrickWaitForTp);
		});
		span[13]=T("BeastMode","Beast Mode","rotation",() => RotationToggleOverrides.Get("BeastMode",c.RotationUseBeastMode),() => "Use Beast Mode - the button Borrow turns into the familiar's kinship action. Quelling Wave is used automatically at range; the others (Soul Crush, the skins, Seedsower, Scouring Ash) are pressed by Reaction Helper when its profile calls for them.",() => {
			Flip(ref c.RotationUseBeastMode);
		});
		span[14]=T("WaveInMelee","Wave in melee","rotation",() => RotationToggleOverrides.Get("WaveInMelee",c.BeastModeQuellingWaveInMelee),() => "Fire Quelling Wave in melee too. Off by default: it is 350 potency, and comboed Axeblade Bite and Shieldsplitter are 500 and 600, so the ordinary combo is simply better up close.",() => {
			Flip(ref c.BeastModeQuellingWaveInMelee);
		},() => c.RotationUseBeastMode);
		span[15]=T("CloudSkim","Cloud Skim","rotation",() => RotationToggleOverrides.Get("CloudSkim",c.BeastModeAllowCloudSkim),() => "Allow Cloud Skim, which is a dash. Off by default - it moves your character, and a rotation that relocates you without being asked is worse than one that does not.",() => {
			Flip(ref c.BeastModeAllowCloudSkim);
		},() => c.RotationUseBeastMode);
		span[16]=T("AutoSummon","Auto-summon","familiar",() => RotationToggleOverrides.Get("AutoSummon",c.RotationAutoSummon),() => "Re-summon a familiar whenever none is out, in or out of combat.",() => {
			Flip(ref c.RotationAutoSummon);
		});
		span[17]=T("Cycle","Cycle","familiar",() => RotationToggleOverrides.Get("Cycle",c.RotationCycleFamiliars),() => "Dismiss the current familiar with Parting Blow when its turn is done and summon the next. Needs Auto rather than a pinned Battlehorn.",() => {
			Flip(ref c.RotationCycleFamiliars);
		});
		span[18]=T("KinOrder","Kin order","familiar",() => c.BattlehornOrder==BattlehornOrder.KinshipPriority,() => "On: summon whichever ready familiar has the highest-ranked kinship. Off: straight slot order 1-2-3, which keeps the cycle predictable.",() => {
			c.BattlehornOrder=((c.BattlehornOrder!=BattlehornOrder.KinshipPriority)?BattlehornOrder.KinshipPriority:BattlehornOrder.SlotOrder);
			c.Save();
		});
		span[19]=T("KeepCovered","Keep Covered","familiar",() => RotationToggleOverrides.Get("KeepCovered",c.RotationKeepCovered),() => "Re-apply Snarl so the familiar keeps taking the damage meant for you. Held back when a familiar swap is close. When off, Challenge is used instead so enemies stay on you.",() => {
			Flip(ref c.RotationKeepCovered);
		});
		span[20]=T("PartingBlow","Parting Blow","familiar",() => RotationToggleOverrides.Get("PartingBlow",c.RotationAllowPartingBlow),() => "Allow Parting Blow for AoE damage. 1000 potency in a wide radius, but your familiar retreats. Cycling uses it regardless of this switch.",() => {
			Flip(ref c.RotationAllowPartingBlow);
		});
		span[21]=T("NeedVantage","Need Vantage","familiar",() => RotationToggleOverrides.Get("NeedVantage",c.RotationPartingBlowNeedsVantage),() => "Hold Parting Blow until Lingering Vantage is up - it is worth 500 extra potency. Ignored below level 18, when One with Nature is off, and once the familiar is well past its dwell time.",() => {
			Flip(ref c.RotationPartingBlowNeedsVantage);
		});
		span[22]=T("SpendPower","Spend power","familiar",() => RotationToggleOverrides.Get("SpendPower",c.RotationCycleWaitForSpentPower),() => "Wait for the familiar's Beast Power to drop below 100 before Parting Blow, so a Trick is not thrown away with it. Dropped once the familiar is well past its dwell time.",() => {
			Flip(ref c.RotationCycleWaitForSpentPower);
		});
		span[23]=T("Borrow","Borrow","familiar",() => RotationToggleOverrides.NatureChoice(c)==OneWithNatureChoice.Borrow,() => "Use Borrow. Turning this on turns Tempered Release off; both may be off. Reaction Helper switches this per fight.",() => {
			c.SetNatureToggle(OneWithNatureChoice.Borrow,!c.RotationUseOneWithNature||c.OneWithNature!=OneWithNatureChoice.Borrow);
			c.Save();
		});
		span[24]=T("TemperedRelease","Tempered Release","familiar",() => RotationToggleOverrides.NatureChoice(c)==OneWithNatureChoice.TemperedRelease,() => "Use Tempered Release. Turning this on turns Borrow off; both may be off. Reaction Helper switches this per fight.",() => {
			c.SetNatureToggle(OneWithNatureChoice.TemperedRelease,!c.RotationUseOneWithNature||c.OneWithNature!=OneWithNatureChoice.TemperedRelease);
			c.Save();
		});
		return list;
		static QuickToggle T(string key,string label,string panel,Func<bool> get,Func<string> tip,System.Action click,Func<bool>? enabled=null) {
			return new QuickToggle(key,label,panel,get,tip,click,enabled);
		}
	}
	private IEnumerable<QuickToggle> OrderedQuickToggles() {
		List<QuickToggle> all=QuickToggles;
		return from k in BeastmasterConfiguration.Arrange(configuration.QuickToggleOrder,all.Select((QuickToggle t) => t.Key))
			select all.First((QuickToggle t) => t.Key==k);
	}
	private QuickToggleBinding BindingOf(QuickToggle t) {
		return configuration.ToggleBinding(t.Key,t.DefaultPanel);
	}
	private IEnumerable<uint> OrderedHotbar() {
		IEnumerable<string> defaults=BeastmasterHotbar.Slots.Select((uint a) => a.ToString());
		return BeastmasterConfiguration.Arrange(configuration.HotbarOrder,defaults).Select(uint.Parse);
	}
	private void DrawToggleGrid(string panelKey,OverlayPanelSettings panel) {
		ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(configuration.QtHSpacing,configuration.QtVSpacing));
		BeginGrid(panel);
		foreach(QuickToggle item in OrderedQuickToggles()) {
			QuickToggleBinding quickToggleBinding=BindingOf(item);
			if(quickToggleBinding.Show&&string.Equals(quickToggleBinding.Panel,panelKey,StringComparison.Ordinal)) {
				GridToggle(item.Label,item.Get(),item.Tip(),item.Click,item.Enabled?.Invoke() ?? true);
			}
		}
		EndGrid();
		ImGui.PopStyleVar();
	}
	private unsafe bool DragLabel(string label,string tip,string key,List<string> order) {
		bool result=false;
		ImGui.AlignTextToFramePadding();
		ImU8String label2=new ImU8String(10,2);
		label2.AppendLiteral(":: ");
		label2.AppendFormatted(label);
		label2.AppendLiteral("##drag_");
		label2.AppendFormatted(key);
		ImGui.Selectable(label2,selected: false,ImGuiSelectableFlags.None,new Vector2(190f,0f));
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip(tip+"\nDrag up or down to change its place on the overlay.");
		}
		if(ImGui.BeginDragDropSource()) {
			dragging=key;
			ImGui.SetDragDropPayload("bmh-order",null);
			ImGui.TextUnformatted(label);
			ImGui.EndDragDropSource();
		}
		if(ImGui.BeginDragDropTarget()) {
			if(ImGui.AcceptDragDropPayload("bmh-order").Handle!=null) {
				string text=dragging;
				if(text!=null&&text!=key) {
					int num=order.IndexOf(text);
					int num2=order.IndexOf(key);
					if(num>=0&&num2>=0) {
						order.RemoveAt(num);
						order.Insert(num2,text);
						result=true;
					}
					dragging=null;
				}
			}
			ImGui.EndDragDropTarget();
		}
		return result;
	}
	private static bool Bindable(VirtualKey k) {
		bool flag=((k<=VirtualKey.RBUTTON||k-4<=VirtualKey.RBUTTON||k-160<=VirtualKey.XBUTTON1)?true:false);
		return !flag;
	}
	private static string KeyName(IEnumerable<int> keys) {
		List<VirtualKey> list=keys.Select((int k) => (VirtualKey)k).ToList();
		if(list.Count!=0) {
			return string.Join("+",from k in list
				orderby Modifiers.Contains(k) descending
				select k.GetFancyName());
		}
		return "None";
	}
	private void PollCapture() {
		string text=capturing;
		if(text==null) {
			return;
		}
		List<VirtualKey> list=(from k in DalamudApi.KeyState.GetValidVirtualKeys()
			where Bindable(k)&&DalamudApi.KeyState[k]
			select k).ToList();
		if(list.Contains(VirtualKey.ESCAPE)) {
			capturing=null;
			peak.Clear();
			return;
		}
		foreach(VirtualKey item in list) {
			peak.Add(item);
		}
		if(list.Count==0&&peak.Count>0) {
			List<int> keys=peak.Select((VirtualKey k) => (int)k).ToList();
			if(text.StartsWith("hb:",StringComparison.Ordinal)) {
				configuration.HotbarBinding(uint.Parse(text.Substring(3))).Keys=keys;
			}
			else {
				configuration.ToggleBinding(text,string.Empty).Keys=keys;
			}
			configuration.Save();
			capturing=null;
			peak.Clear();
		}
	}
	private void PollHotkeys() {
		if(capturing!=null||ImGui.GetIO().WantTextInput||!DalamudApi.ClientState.IsLoggedIn) {
			return;
		}
		foreach(QuickToggle quickToggle in QuickToggles) {
			List<int> keys=BindingOf(quickToggle).Keys;
			if(keys.Count>0&&Edge(quickToggle.Key,keys)) {
				Func<bool>? enabled=quickToggle.Enabled;
				if(enabled==null||enabled()) {
					quickToggle.Click();
				}
			}
		}
		uint[] slots=BeastmasterHotbar.Slots;
		for(int i=0;i<slots.Length;i++) {
			uint actionId=slots[i];
			List<int> keys2=configuration.HotbarBinding(actionId).Keys;
			if(keys2.Count>0&&Edge("hb:"+actionId,keys2)) {
				BeastmasterActionRequest.Toggle(actionId);
			}
		}
	}
	private bool Edge(string key,List<int> keys) {
		bool flag=keys.Count>0&&ComboDown(keys);
		wasDown.TryGetValue(key,out var value);
		wasDown[key]=flag;
		if(flag) {
			return !value;
		}
		return false;
	}
	private static bool ComboDown(List<int> keys) {
		HashSet<VirtualKey> bound=keys.Select((int k) => (VirtualKey)k).ToHashSet();
		if(bound.All((VirtualKey k) => DalamudApi.KeyState[k])) {
			return Modifiers.All((VirtualKey m) => bound.Contains(m)||!DalamudApi.KeyState[m]);
		}
		return false;
	}
	private bool DrawQuickToggleSettings() {
		bool result=false;
		if(ImGui.BeginChild("##qt-sections",new Vector2(150f,0f))) {
			string[] array=new string[2] { "Customize", "Keybinds" };
			foreach(string text in array) {
				bool flag=text==toggleSettingsSection;
				if(flag) {
					ImGui.PushStyleColor(ImGuiCol.Button,ImGui.GetStyle().Colors[22]);
				}
				ImU8String label=new ImU8String(8,2);
				label.AppendFormatted(text);
				label.AppendLiteral("##qtsec_");
				label.AppendFormatted(text);
				if(ImGui.Button(label,new Vector2(-1f,26f))) {
					toggleSettingsSection=text;
				}
				if(flag) {
					ImGui.PopStyleColor();
				}
			}
		}
		ImGui.EndChild();
		ImGui.SameLine();
		if(ImGui.BeginChild("##qt-rows",Vector2.Zero)) {
			result=((toggleSettingsSection=="Keybinds")?DrawToggleKeybinds():DrawToggleCustomize());
		}
		ImGui.EndChild();
		return result;
	}
	private bool DrawToggleCustomize() {
		bool flag=false;
		ImGui.TextWrapped("Each window below is its own overlay you can drag where you like. Toggles are listed in the Keybinds section: drag them to reorder, move one to the other window, hide it, or give it a key.");
		ImGui.Spacing();
		string[] array=new string[2] { "rotation", "familiar" };
		foreach(string text in array) {
			OverlayPanelSettings overlayPanelSettings=configuration.Panel(text);
			ImU8String strId=new ImU8String(9,1);
			strId.AppendLiteral("qt-panel-");
			strId.AppendFormatted(text);
			ImGui.PushID(strId);
			bool v=overlayPanelSettings.Show;
			ImU8String label=new ImU8String(16,1);
			label.AppendLiteral("Show the ");
			label.AppendFormatted(RotationPanels.TitleOf(text));
			label.AppendLiteral(" window");
			if(ImGui.Checkbox(label,ref v)) {
				overlayPanelSettings.Show=v;
				flag=true;
			}
			ImGui.SameLine();
			bool v2=overlayPanelSettings.Locked;
			if(ImGui.Checkbox("Locked",ref v2)) {
				overlayPanelSettings.Locked=v2;
				flag=true;
			}
			SettingTip("Stop this window being dragged, once it is where you want it.");
			ImGui.SameLine();
			if(ImGui.Button("Reset position")) {
				OverlayPanelSettings overlayPanelSettings2=RotationPanels.Defaults(text);
				overlayPanelSettings.PositionX=overlayPanelSettings2.PositionX;
				overlayPanelSettings.PositionY=overlayPanelSettings2.PositionY;
				flag=true;
			}
			SettingTip("Put this window back where it started - useful if it has ended up off-screen.");
			float v3=overlayPanelSettings.Width;
			ImGui.SetNextItemWidth(220f);
			if(ImGui.SliderFloat("Width",ref v3,140f,900f,"%.0f")) {
				overlayPanelSettings.Width=Math.Clamp(v3,140f,900f);
				flag=true;
			}
			int v4=overlayPanelSettings.Columns;
			ImGui.SetNextItemWidth(220f);
			if(ImGui.SliderInt("Columns",ref v4,1,6)) {
				overlayPanelSettings.Columns=Math.Clamp(v4,1,6);
				flag=true;
			}
			ImGui.PopID();
			ImGui.Separator();
		}
		int v5=configuration.QtHSpacing;
		ImGui.SetNextItemWidth(220f);
		if(ImGui.SliderInt("Horizontal spacing",ref v5,0,30)) {
			configuration.QtHSpacing=v5;
			flag=true;
		}
		int v6=configuration.QtVSpacing;
		ImGui.SetNextItemWidth(220f);
		if(ImGui.SliderInt("Vertical spacing",ref v6,0,30)) {
			configuration.QtVSpacing=v6;
			flag=true;
		}
		int v7=configuration.QtButtonHeight;
		ImGui.SetNextItemWidth(220f);
		if(ImGui.SliderInt("Button height (0 = automatic)",ref v7,0,60)) {
			configuration.QtButtonHeight=v7;
			flag=true;
		}
		float v8=configuration.QtTextScale;
		ImGui.SetNextItemWidth(220f);
		if(ImGui.SliderFloat("Text scale",ref v8,0.6f,2f,"%.2f")) {
			configuration.QtTextScale=v8;
			flag=true;
		}
		float v9=configuration.OverlayBgAlpha;
		ImGui.SetNextItemWidth(220f);
		if(ImGui.SliderFloat("Background opacity",ref v9,0f,1f,"%.2f")) {
			configuration.OverlayBgAlpha=v9;
			flag=true;
		}
		Vector4 col=configuration.QtEnabled;
		if(ImGui.ColorEdit4("Colour when on",ref col,ImGuiColorEditFlags.AlphaBar)) {
			configuration.QtEnabled=col;
			flag=true;
		}
		Vector4 col2=configuration.QtDisabled;
		if(ImGui.ColorEdit4("Colour when off",ref col2,ImGuiColorEditFlags.AlphaBar)) {
			configuration.QtDisabled=col2;
			flag=true;
		}
		if(flag) {
			configuration.Save();
		}
		return false;
	}
	private bool DrawToggleKeybinds() {
		bool flag=false;
		PollCapture();
		ImGui.PushStyleColor(ImGuiCol.Text,ImGui.GetStyle().Colors[1]);
		ImGui.TextWrapped("Drag a toggle by its name to move it up or down - the overlay buttons follow this order. Window: 1 = Rotation, 2 = Familiar. Click a key box, then press the combination (right click clears).");
		ImGui.PopStyleColor();
		List<string> list=BeastmasterConfiguration.Arrange(configuration.QuickToggleOrder,QuickToggles.Select((QuickToggle t) => t.Key));
		bool moved=false;
		foreach(string key in list.ToList()) {
			QuickToggle quickToggle=QuickToggles.First((QuickToggle x) => x.Key==key);
			ImGui.PushID(key);
			flag|=KeybindRow(quickToggle.Label,quickToggle.Tip(),key,BindingOf(quickToggle),isToggle: true,list,ref moved,null,quickToggle);
			ImGui.PopID();
		}
		if(moved) {
			configuration.QuickToggleOrder=list;
			flag=true;
		}
		if(flag) {
			configuration.Save();
		}
		return false;
	}
	private bool KeybindRow(string label,string tip,string key,QuickToggleBinding bind,bool isToggle,List<string> order,ref bool moved,string? orderKey=null,QuickToggle? toggle=null) {
		bool result=false;
		moved|=DragLabel(label,tip,orderKey ?? key,order);
		ImGui.SameLine(210f);
		ImGui.SetNextItemWidth(36f);
		if(isToggle) {
			int data=((!(bind.Panel=="familiar"))?1:2);
			if(ImGui.InputInt("##window",ref data)) {
				bind.Panel=((Math.Clamp(data,1,2)==2)?"familiar":"rotation");
				result=true;
			}
		}
		else {
			ImGui.BeginDisabled();
			int data2=0;
			ImGui.InputInt("##window",ref data2);
			ImGui.EndDisabled();
		}
		ImGui.SameLine();
		string text=(isToggle?key:("hb:"+key));
		bool flag=capturing==text;
		ImU8String label2=new ImU8String(5,1);
		label2.AppendFormatted(flag?"Press keys...":KeyName(bind.Keys));
		label2.AppendLiteral("##key");
		if(ImGui.Button(label2,new Vector2(150f,0f))) {
			capturing=text;
			peak.Clear();
		}
		if(ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
			bind.Keys=new List<int>();
			result=true;
			if(flag) {
				capturing=null;
			}
		}
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Click, then press the key or combination (e.g. ctrl+1). Taken when released. Escape cancels, right click clears.");
		}
		ImGui.SameLine();
		bool v=bind.Show;
		if(ImGui.Checkbox("Show Button##show",ref v)) {
			bind.Show=v;
			result=true;
		}
		if(toggle!=null) {
			ImGui.SameLine();
			bool v2=toggle.Get();
			ImGui.BeginDisabled(!(toggle.Enabled?.Invoke() ?? true));
			if(ImGui.Checkbox("Enabled##on",ref v2)&&v2!=toggle.Get()) {
				toggle.Click();
			}
			ImGui.EndDisabled();
			if(ImGui.IsItemHovered()) {
				ImGui.SetTooltip("The toggle's own state: ticked = on. Same as clicking its button on the overlay.");
			}
		}
		return result;
	}
	private bool DrawHotbarSettings() {
		bool result=false;
		if(ImGui.BeginChild("##hb-sections",new Vector2(150f,0f))) {
			string[] array=new string[2] { "Customize", "Keybinds" };
			foreach(string text in array) {
				bool flag=text==hotbarSettingsSection;
				if(flag) {
					ImGui.PushStyleColor(ImGuiCol.Button,ImGui.GetStyle().Colors[22]);
				}
				ImU8String label=new ImU8String(8,2);
				label.AppendFormatted(text);
				label.AppendLiteral("##hbsec_");
				label.AppendFormatted(text);
				if(ImGui.Button(label,new Vector2(-1f,26f))) {
					hotbarSettingsSection=text;
				}
				if(flag) {
					ImGui.PopStyleColor();
				}
			}
		}
		ImGui.EndChild();
		ImGui.SameLine();
		if(ImGui.BeginChild("##hb-rows",Vector2.Zero)) {
			result=((hotbarSettingsSection=="Keybinds")?DrawHotbarKeybinds():DrawHotbarCustomize());
		}
		ImGui.EndChild();
		return result;
	}
	private bool DrawHotbarCustomize() {
		bool flag=false;
		OverlayPanelSettings overlayPanelSettings=configuration.Panel("hotbar");
		bool v=overlayPanelSettings.Show;
		if(ImGui.Checkbox("Show the Actions window",ref v)) {
			overlayPanelSettings.Show=v;
			flag=true;
		}
		ImGui.SameLine();
		bool v2=overlayPanelSettings.Locked;
		if(ImGui.Checkbox("Locked",ref v2)) {
			overlayPanelSettings.Locked=v2;
			flag=true;
		}
		ImGui.SameLine();
		if(ImGui.Button("Reset position")) {
			OverlayPanelSettings overlayPanelSettings2=RotationPanels.Defaults("hotbar");
			overlayPanelSettings.PositionX=overlayPanelSettings2.PositionX;
			overlayPanelSettings.PositionY=overlayPanelSettings2.PositionY;
			flag=true;
		}
		int v3=overlayPanelSettings.Columns;
		ImGui.SetNextItemWidth(220f);
		if(ImGui.SliderInt("Icons per row",ref v3,1,6)) {
			overlayPanelSettings.Columns=Math.Clamp(v3,1,6);
			flag=true;
		}
		float v4=configuration.RotationOverlayIconSize;
		ImGui.SetNextItemWidth(220f);
		if(ImGui.SliderFloat("Icon size",ref v4,20f,64f,"%.0f px")) {
			configuration.RotationOverlayIconSize=Math.Clamp(v4,20f,64f);
			flag=true;
		}
		SettingTip("Click an ability on the Actions panel to have the rotation use it next. It rings green while the request is outstanding and clears as soon as the action goes through; click the same icon again to cancel, and a request that never becomes usable lapses after 30s.");
		if(flag) {
			configuration.Save();
		}
		return false;
	}
	private bool DrawHotbarKeybinds() {
		bool flag=false;
		PollCapture();
		ImGui.PushStyleColor(ImGuiCol.Text,ImGui.GetStyle().Colors[1]);
		ImGui.TextWrapped("Drag an action by its name to move it up or down - the Actions window follows this order. Click a key box, then press the combination to request that action (right click clears).");
		ImGui.PopStyleColor();
		List<string> list=BeastmasterConfiguration.Arrange(configuration.HotbarOrder,BeastmasterHotbar.Slots.Select((uint a) => a.ToString()));
		bool moved=false;
		foreach(string item in list.ToList()) {
			uint num=uint.Parse(item);
			string label=BeastmasterGameData.ActionName(num,$"Action {num}");
			ImGui.PushID("hb"+item);
			flag|=KeybindRow(label,"Ask the rotation to use this next.",item,configuration.HotbarBinding(num),isToggle: false,list,ref moved);
			ImGui.PopID();
		}
		if(moved) {
			configuration.HotbarOrder=list;
			flag=true;
		}
		if(flag) {
			configuration.Save();
		}
		return false;
	}
	private void DrawRotationToggleOverlay() {
		DrawRotationShapePanel();
		DrawFamiliarPanel();
		DrawThresholdsPanel();
		DrawHotbarPanel();
	}
	private void DrawRotationShapePanel() {
		if(BeginPanel("rotation",out OverlayPanelSettings panel)) {
			DrawToggleGrid("rotation",panel);
			if(autoCaptureService.TryCapture&&!string.IsNullOrWhiteSpace(autoCaptureService.CaptureState)) {
				ImGui.PushStyleColor(ImGuiCol.Text,ImGui.GetStyle().Colors[1]);
				ImGui.TextWrapped(autoCaptureService.CaptureState);
				ImGui.PopStyleColor();
			}
			EndPanel(panel);
		}
	}
	private void SetAiAssist(bool enabled) {
		rotationRunner.SetEnabled(enabled);
		if(enabled&&autoCaptureService.IsEnabled) {
			autoCaptureService.SetEnabled(enabled: false);
		}
	}
	private void DrawFamiliarPanel() {
		if(!BeginPanel("familiar",out OverlayPanelSettings panel)) {
			return;
		}
		DrawToggleGrid("familiar",panel);
		int v=(int)configuration.RotationBurnBelowEnemyHpPercent;
		ImGui.SetNextItemWidth(140f);
		if(ImGui.SliderInt("Burn when the enemy is under (% HP, 0 = off)##bmh-burn-below",ref v,0,60)) {
			configuration.RotationBurnBelowEnemyHpPercent=v;
			configuration.Save();
		}
		if(ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Turns Burn on by itself once the target has less than this share of its HP left. 0 leaves it to the Burn switch.");
		}
		string encounter=rotationRunner.BossModEncounter;
		if(rotationRunner.BossModPresent&&encounter.Length>0&&(configuration.ShieldChargeGapClose||configuration.ShieldChargeAoe)) {
			if(configuration.ShieldChargeBlockedEncounters.Any((string name) => name.Trim().Equals(encounter,StringComparison.OrdinalIgnoreCase))) {
				ImU8String label=new ImU8String(19,1);
				label.AppendLiteral("Allow Gap Close in ");
				label.AppendFormatted(encounter);
				if(ImGui.Button(label)) {
					configuration.ShieldChargeBlockedEncounters.RemoveAll((string name) => name.Trim().Equals(encounter,StringComparison.OrdinalIgnoreCase));
					configuration.Save();
				}
				Tip("Shield Charge is currently refused for the whole of "+encounter+". Allow it again and the usual per-dash safety check decides each one.");
			}
			else {
				ImU8String label2=new ImU8String(19,1);
				label2.AppendLiteral("Never Gap Close in ");
				label2.AppendFormatted(encounter);
				if(ImGui.Button(label2)) {
					configuration.ShieldChargeBlockedEncounters.Add(encounter);
					configuration.Save();
					Tip("Refuse Shield Charge for the whole of "+encounter+".");
				}
			}
		}
		EndPanel(panel);
	}
	private void DrawThresholdsPanel() {
		if(!BeginPanel("thresholds",out OverlayPanelSettings panel)) {
			return;
		}
		float x=ImGui.GetContentRegionAvail().X;
		if(autoCaptureService.TryCapture) {
			float v=configuration.CaptureHpThreshold;
			ImGui.SetNextItemWidth(x);
			if(ImGui.SliderFloat("##bmh-capture-hp",ref v,1f,100f,"Capture below %.0f%% HP")) {
				configuration.CaptureHpThreshold=Math.Clamp(v,1f,100f);
				configuration.Save();
			}
			Tip("Capture is attempted once the target drops below this share of its HP. Success rate rises as its HP falls.");
		}
		int v2=configuration.RotationAxeTpThreshold;
		ImGui.SetNextItemWidth(x);
		if(ImGui.SliderInt("##bmh-axe-tp",ref v2,100,250,"Axe at %d TP")) {
			configuration.RotationAxeTpThreshold=Math.Clamp(v2,100,250);
			configuration.Save();
		}
		Tip("Instinctual axes consume ALL your TP and scale from 400 potency at 100 TP to 1000 at 250, so the rotation holds until TP reaches this. Burn ignores it.");
		if(configuration.RotationUseCooldowns) {
			int v3=configuration.RotationRallyMinStacks;
			ImGui.SetNextItemWidth(x);
			if(ImGui.SliderInt("##bmh-rally",ref v3,1,3,"Rally at %d Mastered")) {
				configuration.RotationRallyMinStacks=Math.Clamp(v3,1,3);
				configuration.Save();
			}
			Tip("Rally gives 40 TP plus 70 per stack on a 120s cooldown, so 3 stacks is a full 250 bar and 1 stack is 110. Burn ignores it.");
			int v4=configuration.RotationCheerMinStacks;
			ImGui.SetNextItemWidth(x);
			if(ImGui.SliderInt("##bmh-cheer",ref v4,1,3,"Cheer at %d Natural")) {
				configuration.RotationCheerMinStacks=Math.Clamp(v4,1,3);
				configuration.Save();
			}
			Tip("Same arithmetic for your familiar: 30 Familiar TP plus 70 per Natural Instinct stack.");
			int v5=configuration.RotationRallyStallSeconds;
			ImGui.SetNextItemWidth(x);
			if(ImGui.SliderInt("##bmh-stall",ref v5,0,90,(v5==0)?"Never settle for fewer":"Settle for fewer after %ds")) {
				configuration.RotationRallyStallSeconds=Math.Clamp(v5,0,90);
				configuration.Save();
			}
			int v6=configuration.RotationUniversalityHoldSeconds;
			ImGui.SetNextItemWidth(x);
			if(ImGui.SliderInt("##bmh-universality-hold",ref v6,0,60,(v6==0)?"Never hold Rally for Universality":"Hold Rally up to %ds for Universality")) {
				configuration.RotationUniversalityHoldSeconds=Math.Clamp(v6,0,60);
				configuration.Save();
			}
			Tip("Universality (the Infinitive finisher) needs Rally to go off while a Sunstrider/Moonstalker window is open. At 0, Rally is pressed as normal and only becomes the finisher if a window happens to be open at that moment - no damage is given up waiting. Above 0, Rally is held back that long for a window to appear: each Universality is worth more than a plain axe, but every second held is Rally uptime lost, so it can cost more than it gains.");
			Tip("Three stacks takes about 80s to build and Rally recharges in 120s, so in a long fight the cooldown is the limit and waiting for three is free. In a short one - trash, capture farming - holding out for three means Rally is never pressed at all. After this long ready and waiting, it fires on whatever stacks it has. Zero waits forever.");
		}
		EndPanel(panel);
	}
	private void DrawHotbarPanel() {
		ImGuiStylePtr style=ImGui.GetStyle();
		float rotationOverlayIconSize=configuration.RotationOverlayIconSize;
		float num=rotationOverlayIconSize;
		int num2=Math.Clamp(configuration.Panel("hotbar").Columns,1,BeastmasterHotbar.Slots.Length);
		float value=style.WindowPadding.X*2f+num*(float)num2+style.ItemSpacing.X*(float)(num2-1);
		if(!BeginPanel("hotbar",out OverlayPanelSettings panel,value)) {
			return;
		}
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			ImGui.TextDisabled("Waiting for the character to load");
			EndPanel(panel);
			return;
		}
		byte level=localPlayer.Level;
		ulong targetId=DalamudApi.TargetManager.Target?.GameObjectId ?? localPlayer.GameObjectId;
		string nextActionName=rotationRunner.NextActionName;
		Vector2 iconSize=new Vector2(rotationOverlayIconSize,rotationOverlayIconSize);
		BeastmasterCatalogEntry summonEntry=BeastmasterGaugeSnapshot.Read().SummonEntry;
		BeastKin kin=((!(summonEntry==null))?BeastmasterKinship.KinOf(summonEntry.Number):BeastKin.Unknown);
		BeastKin borrowedKinship=rotationRunner.BorrowedKinship;
		int num3=0;
		int num4=-1;
		foreach(uint item in OrderedHotbar()) {
			num4++;
			if(!configuration.HotbarBinding(item).Show||(BeastmasterDutyActions.IsDutyAction(item)&&!BeastmasterDutyActions.Read(item).Present)) {
				continue;
			}
			uint num5=item switch {
				44886u => BeastmasterActions.KinshipActionFor(borrowedKinship),
				44895u => BeastmasterActions.BorrowFor(kin),
				_ => item,
			};
			uint num6=BeastmasterGameData.ActionIcon(num5);
			if(num6!=0) {
				HotbarSlotState state=BeastmasterHotbar.Read(item,level,targetId);
				if(num3%num2!=0) {
					ImGui.SameLine();
				}
				num3++;
				DrawHotbarSlot(num4,item,num5,num6,state,iconSize,nextActionName);
			}
		}
		uint pending=BeastmasterActionRequest.Pending;
		if(pending!=0) {
			string value2=BeastmasterGameData.ActionName(pending,"an action");
			if(!configuration.RotationEnabled) {
				ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(1f,0.8f,0.4f,1f));
				ImU8String text=new ImU8String(58,1);
				text.AppendFormatted(value2);
				text.AppendLiteral(" is requested, but AI Assist is off - nothing will use it.");
				ImGui.TextWrapped(text);
				ImGui.PopStyleColor();
			}
			else {
				ImGui.PushStyleColor(ImGuiCol.Text,ToggleOnHover);
				ImU8String text2=new ImU8String(15,2);
				text2.AppendLiteral("Waiting on ");
				text2.AppendFormatted(value2);
				text2.AppendLiteral(" (");
				text2.AppendFormatted(BeastmasterActionRequest.SecondsRemaining,"0");
				text2.AppendLiteral("s)");
				ImGui.TextWrapped(text2);
				ImGui.PopStyleColor();
			}
			Tip("Click the same icon again to cancel. The request lapses on its own after 30s.");
		}
		EndPanel(panel);
	}
	private void DrawHotbarSlot(int index,uint actionId,uint faceId,uint icon,HotbarSlotState state,Vector2 iconSize,string nextAction) {
		string text=BeastmasterGameData.ActionName(actionId,$"Action {actionId}");
		string value=((faceId==actionId)?text:(text+" ("+BeastmasterGameData.ActionName(faceId,"current")+")"));
		bool flag=BeastmasterActionRequest.IsPending(actionId);
		Vector4 tintCol;
		if(state.Unlocked) {
			tintCol=(state.Usable?SlotReady:SlotCooling);
		}
		else {
			tintCol=SlotLocked;
		}
		ImGui.PushID(index);
		bool flag2=ImGui.ImageButton(DalamudApi.Textures.GetFromGameIcon(new GameIconLookup(icon)).GetWrapOrEmpty().Handle,iconSize,Vector2.Zero,Vector2.One,0,Vector4.Zero,tintCol);
		ImGui.PopID();
		Vector2 itemRectMin=ImGui.GetItemRectMin();
		Vector2 itemRectMax=ImGui.GetItemRectMax();
		ImDrawListPtr windowDrawList=ImGui.GetWindowDrawList();
		if(state.Unlocked&&state.OnCooldown) {
			float y=itemRectMax.Y-(itemRectMax.Y-itemRectMin.Y)*(1f-state.CooldownFraction);
			windowDrawList.AddRectFilled(itemRectMin,new Vector2(itemRectMax.X,y),ImGui.GetColorU32(new Vector4(0f,0f,0f,0.6f)));
		}
		if(flag) {
			windowDrawList.AddRect(itemRectMin,itemRectMax,ImGui.GetColorU32(ToggleOnHover),3f,ImDrawFlags.None,3f);
		}
		else if(state.Unlocked&&string.Equals(text,nextAction,StringComparison.Ordinal)) {
			windowDrawList.AddRect(itemRectMin,itemRectMax,ImGui.GetColorU32(OverlayGold),2f,ImDrawFlags.None,1.5f);
		}
		if(ImGui.IsItemHovered()) {
			string value2;
			if(state.Unlocked) {
				if(state.OnCooldown) {
					value2=$"{state.CooldownRemaining:0.0}s remaining";
				}
				else {
					value2=(state.Usable?"ready":"not usable right now");
				}
			}
			else {
				value2=(BeastmasterDutyActions.IsDutyAction(actionId)?"not offered by this duty":$"unlocks at level {BeastmasterActions.UnlockLevel(actionId)}");
			}
			string value3=actionId switch {
				46751u => " Sends your familiar in to hold enmity and take all damage meant for you, 45s.",
				46750u => " Puts you at the top of the enmity list and cancels your familiar's cover.",
				_ => string.Empty,
			};
			Tip(flag?$"{value} - requested, {value2}. Click again to cancel.{value3}":$"{value} - {value2}. Click to have the rotation use it next.{value3}");
		}
		if(flag2) {
			BeastmasterActionRequest.Toggle(actionId);
		}
	}
	private bool BeginPanel(string key,out OverlayPanelSettings panel,float? exactWidth=null) {
		panel=configuration.Panel(key);
		if(!panel.Show) {
			return false;
		}
		ImGuiWindowFlags imGuiWindowFlags=ImGuiWindowFlags.NoNav|ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoResize|ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoScrollWithMouse|ImGuiWindowFlags.NoFocusOnAppearing;
		if(panel.Locked) {
			imGuiWindowFlags|=ImGuiWindowFlags.NoMove;
		}
		ImGui.SetNextWindowPos(panel.HasPosition?new Vector2(panel.PositionX,panel.PositionY):RotationPanels.DefaultPosition(key),ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowBgAlpha(configuration.OverlayBgAlpha);
		ImGui.SetNextWindowSize(new Vector2(exactWidth ?? panel.Width,0f),ImGuiCond.Always);
		ImU8String name=new ImU8String(11,1);
		name.AppendLiteral("##BmhPanel_");
		name.AppendFormatted(key);
		if(!ImGui.Begin(name,imGuiWindowFlags)) {
			ImGui.End();
			return false;
		}
		if((key=="rotation"||key=="familiar")?true:false) {
			ImGui.SetWindowFontScale(configuration.QtTextScale);
		}
		ImGui.PushTextWrapPos(0f);
		ImGui.TextColored(in OverlayGold,RotationPanels.TitleOf(key));
		ImGui.Separator();
		return true;
	}
	private void EndPanel(OverlayPanelSettings panel) {
		Vector2 windowPos=ImGui.GetWindowPos();
		if(Math.Abs(windowPos.X-panel.PositionX)>1f||Math.Abs(windowPos.Y-panel.PositionY)>1f) {
			panel.PositionX=windowPos.X;
			panel.PositionY=windowPos.Y;
			configuration.Save();
		}
		ImGui.PopTextWrapPos();
		ImGui.End();
	}
	private void BeginGrid(OverlayPanelSettings panel) {
		gridColumn=0;
		gridColumns=Math.Clamp(panel.Columns,1,6);
		float x=ImGui.GetStyle().ItemSpacing.X;
		gridButtonWidth=(ImGui.GetContentRegionAvail().X-x*(float)(gridColumns-1))/(float)gridColumns;
	}
	private void EndGrid() {
		gridColumn=0;
	}
	private void GridToggle(string label,bool value,string tooltip,System.Action onClick,bool enabled=true) {
		if(gridColumn%gridColumns!=0) {
			ImGui.SameLine();
		}
		gridColumn++;
		Vector4 col=(value?configuration.QtEnabled:configuration.QtDisabled);
		Vector4 col2=new Vector4(Math.Min(col.X+0.07f,1f),Math.Min(col.Y+0.07f,1f),Math.Min(col.Z+0.07f,1f),col.W);
		ImGui.PushStyleColor(ImGuiCol.Button,col);
		ImGui.PushStyleColor(ImGuiCol.ButtonHovered,col2);
		ImGui.PushStyleColor(ImGuiCol.ButtonActive,col2);
		ImGui.BeginDisabled(!enabled);
		bool flag=ImGui.Button(label,new Vector2(gridButtonWidth,configuration.QtButtonHeight));
		ImGui.EndDisabled();
		ImGui.PopStyleColor(3);
		Tip(enabled?tooltip:(tooltip+" (unavailable with the switch it depends on turned off)"));
		if(flag&enabled) {
			onClick();
		}
	}
	private void Flip(ref bool value) {
		value=!value;
		configuration.Save();
	}
}
