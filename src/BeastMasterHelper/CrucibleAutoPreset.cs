using System;
namespace Beastmaster;

public static class CrucibleAutoPreset {
	public const string Name="BeastMasterCrucible";
	private const string OldName="Beast Master Helper - Crucible Movement";
	private static bool active;
	private static string? previousActive;
	private static bool weEnabledAi;
	private static bool weSetManualTarget;
	public static bool IsActive => active;
	public static string LastResult { get; private set; }="not attempted yet";
	public static void Sync(BossModBridge bossMod,bool wantActive) {
		if(wantActive==active) {
			return;
		}
		if(wantActive) {
			previousActive=bossMod.ActivePresetName;
			if(previousActive=="BeastMasterCrucible") {
				previousActive=null;
			}
			if(!bossMod.CreatePreset(BuildJson(),overwrite: true)) {
				LastResult="BossMod refused to create the preset - see the Dalamud log for the exception, if any.";
				DalamudApi.ChatGui.PrintError("[Beast Master Helper] Crucible auto-preset: "+LastResult);
				return;
			}
			if(previousActive=="Beast Master Helper - Crucible Movement") {
				previousActive=null;
			}
			bossMod.DeletePreset("Beast Master Helper - Crucible Movement");
			weEnabledAi=!bossMod.AiEnabled;
			if(weEnabledAi&&!bossMod.EnableAi(0)) {
				weEnabledAi=false;
				LastResult="Preset created, but BossMod refused to switch AI on - is BossmodReborn (Tekz Fork) 1.0.57 or newer installed?";
				DalamudApi.ChatGui.PrintError("[Beast Master Helper] Crucible auto-preset: "+LastResult);
				return;
			}
			string text=bossMod.ReadSetting("AIConfig","ManualTarget");
			weSetManualTarget=text!=null&&!text.Contains("true",StringComparison.OrdinalIgnoreCase)&&bossMod.WriteSetting("AIConfig","ManualTarget","true");
			bossMod.ActivatePreset("BeastMasterCrucible");
			if(!bossMod.SetAiPreset("BeastMasterCrucible")) {
				LastResult="AI is on, but BossMod refused to hand it the preset.";
				DalamudApi.ChatGui.PrintError("[Beast Master Helper] Crucible auto-preset: "+LastResult);
			}
			else {
				active=true;
				LastResult=(weEnabledAi?("activated (was "+(previousActive ?? "none")+"), BossMod AI switched on and following you"):("activated (was "+(previousActive ?? "none")+"), BossMod AI was already on - left as is"));
				DalamudApi.ChatGui.Print("[Beast Master Helper] Crucible auto-preset: "+LastResult+".");
			}
		}
		else {
			active=false;
			string text2=previousActive;
			previousActive=null;
			if(weSetManualTarget) {
				bossMod.WriteSetting("AIConfig","ManualTarget","false");
				weSetManualTarget=false;
			}
			if(weEnabledAi) {
				bossMod.DisableAi();
			}
			if(text2!=null) {
				bossMod.ActivatePreset(text2);
			}
			else {
				bossMod.ClearActivePreset();
			}
			LastResult=(weEnabledAi?("deactivated (restored "+(text2 ?? "none")+"), BossMod AI switched off"):("deactivated (restored "+(text2 ?? "none")+"), BossMod AI was already on when this started - left running"));
			DalamudApi.ChatGui.Print("[Beast Master Helper] Crucible auto-preset: "+LastResult+".");
		}
	}
	public static void Reset() {
		active=false;
		previousActive=null;
		weEnabledAi=false;
		weSetManualTarget=false;
	}
	private static string BuildJson() {
		return "{\n  \"Name\": \"BeastMasterCrucible\",\n  \"Modules\": {\n    \"BossMod.Autorotation.MiscAI.StayCloseToTarget\": [\n      { \"Track\": \"range\", \"Option\": \"OnHitbox\" }\n    ],\n    \"BossMod.Autorotation.MiscAI.NormalMovement\": [\n      { \"Track\": \"Destination\", \"Option\": \"Pathfind\" },\n      { \"Track\": \"ForbiddenZoneCushion\", \"Option\": \"None\" },\n      { \"Track\": \"Range\", \"Option\": \"Any\" },\n      { \"Track\": \"DelayMovement\", \"Option\": \"None\" },\n      { \"Track\": \"Cast\", \"Option\": \"Leeway\" }\n    ],\n    \"BossMod.Autorotation.MiscAI.GoToPositional\": [\n      { \"Track\": \"Positional\", \"Option\": \"Any\" }\n    ]\n  }\n}";
	}
}
