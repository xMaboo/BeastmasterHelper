using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
namespace Beastmaster;

public sealed class BeastmasterIpc : IDisposable {
	private sealed record ToggleDef(string Key,string Label,Func<bool> Get,Action<bool> Set);
	private const string Prefix="BeastMasterHelper.";
	private readonly List<ToggleDef> toggles;
	private readonly ICallGateProvider<string> listToggles;
	private readonly ICallGateProvider<string,bool> getToggle;
	private readonly ICallGateProvider<string,bool,bool> setToggle;
	private readonly ICallGateProvider<string> hotbar;
	private readonly ICallGateProvider<string> familiars;
	private readonly ICallGateProvider<string,bool> petCommand;
	private readonly ICallGateProvider<string> familiarCatalog;
	private readonly ICallGateProvider<string> borrowedKinship;
	private readonly ICallGateProvider<string> debugState;
	private readonly ICallGateProvider<float> borrowedRemaining;
	private readonly ICallGateProvider<string,bool,bool> setHotbar;
	private readonly ICallGateProvider<uint,float,bool> holdAction;
	private readonly ICallGateProvider<string,string,bool,float,bool> overrideToggle;
	private readonly ICallGateProvider<string,bool> releaseOverride;
	private readonly ICallGateProvider<bool> isEnabled;
	private readonly ICallGateProvider<bool,bool> setEnabled;
	private readonly ICallGateProvider<string,bool> setPositional;
	public BeastmasterIpc(BeastmasterConfiguration c,BeastmasterRotationRunner runner,BeastmasterAutoCaptureService capture) {
		BeastmasterIpc beastmasterIpc=this;
		int num=25;
		List<ToggleDef> list=new List<ToggleDef>(num);
		CollectionsMarshal.SetCount(list,num);
		Span<ToggleDef> span=CollectionsMarshal.AsSpan(list);
		span[0]=new ToggleDef("AiAssist","AI Assist",() => c.RotationEnabled,AiAssist);
		span[1]=new ToggleDef("Capture","Capture",() => capture.TryCapture,capture.SetTryCapture);
		span[2]=new ToggleDef("Aoe","AoE",() => c.RotationUseAoe,(bool v) => {
			c.RotationUseAoe=v;
		});
		span[3]=new ToggleDef("Opener","Opener",() => c.RotationUseOpener,(bool v) => {
			c.RotationUseOpener=v;
		});
		span[4]=new ToggleDef("Cooldowns","Cooldowns",() => c.RotationUseCooldowns,(bool v) => {
			c.RotationUseCooldowns=v;
		});
		span[5]=new ToggleDef("Rally","Rally",() => c.RotationUseRally,(bool v) => {
			c.RotationUseRally=v;
		});
		span[6]=new ToggleDef("RallyingCheer","Rallying Cheer",() => c.RotationUseRallyingCheer,(bool v) => {
			c.RotationUseRallyingCheer=v;
		});
		span[7]=new ToggleDef("PowerActions","Power actions (Trick / axes)",() => c.RotationUsePowerActions,(bool v) => {
			c.RotationUsePowerActions=v;
		});
		span[8]=new ToggleDef("Burn","Burn",() => c.RotationBurn,(bool v) => {
			c.RotationBurn=v;
		});
		span[9]=new ToggleDef("AutoAttack","Auto-attack",() => c.RotationAutoAttack,(bool v) => {
			c.RotationAutoAttack=v;
		});
		span[10]=new ToggleDef("GapClose","Gap close",() => c.ShieldChargeGapClose,(bool v) => {
			c.ShieldChargeGapClose=v;
		});
		span[11]=new ToggleDef("ChargeAoe","Charge AoE",() => c.ShieldChargeAoe,(bool v) => {
			c.ShieldChargeAoe=v;
		});
		span[12]=new ToggleDef("TrickWaitsTp","Trick waits TP",() => c.RotationTrickWaitForTp,(bool v) => {
			c.RotationTrickWaitForTp=v;
		});
		span[13]=new ToggleDef("BeastMode","Beast Mode",() => c.RotationUseBeastMode,(bool v) => {
			c.RotationUseBeastMode=v;
		});
		span[14]=new ToggleDef("WaveInMelee","Wave in melee",() => c.BeastModeQuellingWaveInMelee,(bool v) => {
			c.BeastModeQuellingWaveInMelee=v;
		});
		span[15]=new ToggleDef("CloudSkim","Cloud Skim",() => c.BeastModeAllowCloudSkim,(bool v) => {
			c.BeastModeAllowCloudSkim=v;
		});
		span[16]=new ToggleDef("AutoSummon","Auto-summon",() => c.RotationAutoSummon,(bool v) => {
			c.RotationAutoSummon=v;
		});
		span[17]=new ToggleDef("Cycle","Cycle",() => c.RotationCycleFamiliars,(bool v) => {
			c.RotationCycleFamiliars=v;
		});
		span[18]=new ToggleDef("KeepCovered","Keep Covered",() => c.RotationKeepCovered,(bool v) => {
			c.RotationKeepCovered=v;
		});
		span[19]=new ToggleDef("PartingBlow","Parting Blow",() => c.RotationAllowPartingBlow,(bool v) => {
			c.RotationAllowPartingBlow=v;
		});
		span[20]=new ToggleDef("NeedVantage","Need Vantage",() => c.RotationPartingBlowNeedsVantage,(bool v) => {
			c.RotationPartingBlowNeedsVantage=v;
		});
		span[21]=new ToggleDef("SpendPower","Spend power",() => c.RotationCycleWaitForSpentPower,(bool v) => {
			c.RotationCycleWaitForSpentPower=v;
		});
		span[22]=new ToggleDef("Borrow","Borrow",() => c.RotationUseOneWithNature&&c.OneWithNature==OneWithNatureChoice.Borrow,(bool v) => {
			c.SetNatureToggle(OneWithNatureChoice.Borrow,v);
		});
		span[23]=new ToggleDef("TemperedRelease","Tempered Release",() => c.RotationUseOneWithNature&&c.OneWithNature==OneWithNatureChoice.TemperedRelease,(bool v) => {
			c.SetNatureToggle(OneWithNatureChoice.TemperedRelease,v);
		});
		span[24]=new ToggleDef("OneWithNature","One w/ Nature",() => c.RotationUseOneWithNature,(bool v) => {
			c.RotationUseOneWithNature=v;
		});
		toggles=list;
		IDalamudPluginInterface pluginInterface=DalamudApi.PluginInterface;
		listToggles=pluginInterface.GetIpcProvider<string>("BeastMasterHelper.Toggles");
		listToggles.RegisterFunc(() => string.Join("\n",beastmasterIpc.toggles.Select((ToggleDef t) => $"{t.Key}\t{t.Label}\t{(Effective(t)?1:0)}")));
		getToggle=pluginInterface.GetIpcProvider<string,bool>("BeastMasterHelper.GetToggle");
		getToggle.RegisterFunc((string key) => {
			ToggleDef toggleDef=beastmasterIpc.Find(key);
			return (object)toggleDef!=null&&Effective(toggleDef);
		});
		setToggle=pluginInterface.GetIpcProvider<string,bool,bool>("BeastMasterHelper.SetToggle");
		setToggle.RegisterFunc((string key,bool on) => {
			ToggleDef toggleDef=beastmasterIpc.Find(key);
			if((object)toggleDef==null) {
				return false;
			}
			if(toggleDef.Get()!=on) {
				toggleDef.Set(on);
				c.Save();
			}
			return true;
		});
		overrideToggle=pluginInterface.GetIpcProvider<string,string,bool,float,bool>("BeastMasterHelper.OverrideToggle");
		overrideToggle.RegisterFunc(RotationToggleOverrides.Renew);
		releaseOverride=pluginInterface.GetIpcProvider<string,bool>("BeastMasterHelper.ReleaseToggleOverride");
		releaseOverride.RegisterFunc(RotationToggleOverrides.Release);
		familiars=pluginInterface.GetIpcProvider<string>("BeastMasterHelper.Familiars");
		familiars.RegisterFunc(() => {
			int num2=BeastmasterGaugeSnapshot.Read().SummonEntry?.Number ?? 0;
			List<string> list2=new List<string>();
			for(int i=0;i<3;i++) {
				BeastmasterCatalogEntry beastmasterCatalogEntry=BeastmasterTeam.SlotEntry(i);
				if((object)beastmasterCatalogEntry==null) {
					continue;
				}
				float num3;
				if(beastmasterCatalogEntry.Number!=num2) {
					num3=runner.FamiliarHealthOf(beastmasterCatalogEntry.Number);
				}
				else {
					IPlayerCharacter owner=DalamudApi.ObjectTable.LocalPlayer;
					if(owner!=null) {
						IBattleNpc battleNpc=DalamudApi.ObjectTable.OfType<IBattleNpc>().FirstOrDefault((IBattleNpc npc) => npc.OwnerId==owner.GameObjectId&&npc.MaxHp!=0);
						if(battleNpc!=null) {
							num3=100f*(float)battleNpc.CurrentHp/(float)battleNpc.MaxHp;
							goto IL_00bc;
						}
					}
					num3=runner.FamiliarHealthOf(beastmasterCatalogEntry.Number);
				}
				goto IL_00bc;
				IL_00bc:
				float value=num3;
				list2.Add($"{i+1}\t{beastmasterCatalogEntry.Number}\t{beastmasterCatalogEntry.DisplayName}\t{((beastmasterCatalogEntry.Number==num2)?1:0)}\t{value:0.#}");
			}
			return string.Join("\n",list2);
		});
		familiarCatalog=pluginInterface.GetIpcProvider<string>("BeastMasterHelper.FamiliarCatalog");
		familiarCatalog.RegisterFunc(() => string.Join("\n",from e in BeastmasterCatalog.Entries
			orderby e.Number
			select $"{e.Number}\t{e.DisplayName}"));
		borrowedKinship=pluginInterface.GetIpcProvider<string>("BeastMasterHelper.BorrowedKinship");
		borrowedKinship.RegisterFunc(() => {
			BeastKin beastKin=BeastmasterKinship.Borrowed();
			return (beastKin==BeastKin.Unknown)?"None":beastKin.ToString();
		});
		borrowedRemaining=pluginInterface.GetIpcProvider<float>("BeastMasterHelper.BorrowedRemaining");
		borrowedRemaining.RegisterFunc(() => (BeastmasterKinship.Borrowed()!=BeastKin.Unknown)?BeastmasterKinship.BorrowedRemaining():(-1f));
		debugState=pluginInterface.GetIpcProvider<string>("BeastMasterHelper.DebugState");
		debugState.RegisterFunc(() => {
			IStatus status=DalamudApi.ObjectTable.LocalPlayer?.StatusList.FirstOrDefault((IStatus s) => s.StatusId==4601);
			IGameObject target=DalamudApi.TargetManager.Target;
			return string.Join("\n",new string[6] {
				$"AI Assist {(RotationToggleOverrides.Get("AiAssist",c.RotationEnabled)?"on":"off")}, running {runner.Running}, status: {runner.StatusText}, phase {runner.Phase}",
				$"One with Nature: {((status==null)?"NOT up":$"up, {Math.Abs(status.RemainingTime):0.0}s left")}; nature choice now: {RotationToggleOverrides.NatureChoice(c)?.ToString() ?? "off"}; kinship lent: {BeastmasterKinship.Borrowed()}",
				"Overrides held by Reaction Helper: "+((RotationToggleOverrides.Summary.Length==0)?"none":RotationToggleOverrides.Summary),
				$"Target: {target?.Name.TextValue ?? "none"}; BossMod preferred {runner.BossModPreferredTargetId:X}; BossMod encounter name: '{runner.BossModEncounterName}'; fight handed to Reaction Helper: {CrucibleFightPlan.HandedToReactionHelper(c,runner.BossModEncounterName)}",
				"Rotation last reason: "+runner.LastRotationReason,
				"Requests from Reaction Helper: "+runner.Requests.Describe()
			});
		});
		petCommand=pluginInterface.GetIpcProvider<string,bool>("BeastMasterHelper.PetCommand");
		petCommand.RegisterFunc(BeastmasterPetCommands.Send);
		hotbar=pluginInterface.GetIpcProvider<string>("BeastMasterHelper.Hotbar");
		hotbar.RegisterFunc(() => string.Join("\n",BeastmasterHotbar.Slots.Select((uint id) => $"{Label(id)}\t{(BeastmasterActionRequest.IsPending(id)?1:0)}")));
		setHotbar=pluginInterface.GetIpcProvider<string,bool,bool>("BeastMasterHelper.SetHotbar");
		setHotbar.RegisterFunc((string label,bool on) => {
			uint num2=BeastmasterHotbar.Slots.FirstOrDefault((uint s) => string.Equals(Label(s),label,StringComparison.OrdinalIgnoreCase));
			if(num2==0) {
				return false;
			}
			if(BeastmasterActionRequest.IsPending(num2)!=on) {
				BeastmasterActionRequest.Toggle(num2);
			}
			return true;
		});
		holdAction=pluginInterface.GetIpcProvider<uint,float,bool>("BeastMasterHelper.HoldAction");
		holdAction.RegisterFunc((uint id,float seconds) => {
			BeastmasterActionHolds.Hold(id,seconds);
			return id!=0;
		});
		isEnabled=pluginInterface.GetIpcProvider<bool>("BeastMasterHelper.IsEnabled");
		isEnabled.RegisterFunc(() => c.RotationEnabled);
		setEnabled=pluginInterface.GetIpcProvider<bool,bool>("BeastMasterHelper.SetEnabled");
		setEnabled.RegisterFunc((bool on) => {
			AiAssist(on);
			return true;
		});
		setPositional=pluginInterface.GetIpcProvider<string,bool>("BeastMasterHelper.SetPositional");
		setPositional.RegisterFunc((string position) => {
			string[] array=new string[4] { "Any", "Rear", "Flank", "Front" };
			return CrucibleAutoPreset.IsActive&&Array.IndexOf(array,position)>=0&&runner.BossMod.SetTransientStrategy("BeastMasterCrucible","BossMod.Autorotation.MiscAI.GoToPositional","Positional",position);
		});
		void AiAssist(bool on) {
			runner.SetEnabled(on);
			if(on&&capture.IsEnabled) {
				capture.SetEnabled(enabled: false);
			}
		}
		bool Effective(ToggleDef t) {
			string key=t.Key;
			if(key=="Borrow") {
				return RotationToggleOverrides.NatureChoice(c)==OneWithNatureChoice.Borrow;
			}
			if(key=="TemperedRelease") {
				return RotationToggleOverrides.NatureChoice(c)==OneWithNatureChoice.TemperedRelease;
			}
			return RotationToggleOverrides.Get(t.Key,t.Get());
		}
	}
	private ToggleDef? Find(string key) {
		return toggles.FirstOrDefault((ToggleDef t) => string.Equals(t.Key,key,StringComparison.OrdinalIgnoreCase)||string.Equals(t.Label,key,StringComparison.OrdinalIgnoreCase));
	}
	private static string Label(uint actionId) {
		return BeastmasterGameData.ActionName(actionId,$"Action {actionId}");
	}
	public void Dispose() {
		overrideToggle.UnregisterFunc();
		releaseOverride.UnregisterFunc();
		RotationToggleOverrides.Clear();
		listToggles.UnregisterFunc();
		getToggle.UnregisterFunc();
		setToggle.UnregisterFunc();
		hotbar.UnregisterFunc();
		setHotbar.UnregisterFunc();
		holdAction.UnregisterFunc();
		isEnabled.UnregisterFunc();
		setEnabled.UnregisterFunc();
		familiars.UnregisterFunc();
		petCommand.UnregisterFunc();
		familiarCatalog.UnregisterFunc();
		borrowedKinship.UnregisterFunc();
		debugState.UnregisterFunc();
		borrowedRemaining.UnregisterFunc();
		setPositional.UnregisterFunc();
	}
}
