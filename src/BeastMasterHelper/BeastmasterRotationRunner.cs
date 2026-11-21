using System;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace Beastmaster;

public sealed class BeastmasterRotationRunner : IDisposable {
	private readonly BeastmasterConfiguration configuration;
	private readonly BeastmasterRotationService rotation;
	private DateTime nextTickUtc=DateTime.MinValue;
	private DateTime nextCoveredDropUtc=DateTime.MinValue;
	private DateTime moduleLastSeenUtc=DateTime.MinValue;
	private DateTime nextActionUtc=DateTime.MinValue;
	private int pendingSummonSlot;
	private DateTime pendingSummonExpiresUtc=DateTime.MinValue;
	private readonly BossModBridge bossMod;
	public ReactionRequests Requests { get; }
	public bool OutOfRange { get; private set; }
	public BossModBridge BossMod => bossMod;
	public string StatusText { get; private set; }="Rotation off";
	public string NextActionName { get; private set; }="-";
	public string NextActionReason { get; private set; }="-";
	public string Phase { get; private set; }="Idle";
	public int EnemiesNearTarget { get; private set; }
	public InstinctAffinity HeldHeart => BeastmasterStatusReader.HeldHeart();
	public InstinctAffinity WantedAffinity => BeastmasterActions.NextInWheel(HeldHeart);
	public uint HeldInfinitiveHalf => BeastmasterStatusReader.InfinitiveHalf();
	public bool InfinitiveReady {
		get {
			uint heldInfinitiveHalf=HeldInfinitiveHalf;
			if(heldInfinitiveHalf!=0) {
				return BeastmasterActions.IntentionalHalf(WantedAffinity)==BeastmasterActions.OppositeHalf(heldInfinitiveHalf);
			}
			return false;
		}
	}
	public BeastKin BorrowedKinship => BeastmasterKinship.Borrowed();
	public string CycleStatus { get; private set; }="Not evaluated";
	public string CoveredStatus => rotation.CoveredStatus;
	public string LastRallyNote => rotation.LastRallyNote;
	public int UniversalitySession => rotation.UniversalitySession;
	public DateTime LastRallyAt => rotation.LastRallyAt;
	public string GapCloseStatus => rotation.GapCloseStatus;
	public string LastRotationReason => rotation.LastReason;
	public ulong BossModPreferredTargetId => bossMod.PreferredTargetID;
	public string BossModEncounterName => bossMod.EncounterName;
	public bool BossModPresent => bossMod.IsInstalled;
	public bool BossModDodging => bossMod.IsDodging;
	public string BossModEncounter {
		get {
			if(!bossMod.InEncounter) {
				return string.Empty;
			}
			return bossMod.EncounterName;
		}
	}
	public string CycleDetail { get; private set; }="The cycle has not been evaluated yet.";
	public bool Running { get; private set; }
	public bool IsEnabled => RotationToggleOverrides.Get("AiAssist",configuration.RotationEnabled);
	public BeastmasterRotationRunner(BeastmasterConfiguration configuration,BeastmasterRotationService rotation,BossModBridge bossMod) {
		this.configuration=configuration;
		this.rotation=rotation;
		this.bossMod=bossMod;
		Requests=new ReactionRequests("BeastMasterHelper.",43u,"BST",() => RotationToggleOverrides.Get("AiAssist",configuration.RotationEnabled),(uint action) => BeastmasterActionHolds.IsHeld(action)||!RotationToggleOverrides.AllowsAction(configuration,action)||(BeastmasterActions.Battlehorns.Contains(action)&&(BeastmasterRotationService.AnyFamiliarOut||rotation.SummonGuardActive)));
		uint[] array=new uint[21] {
			44879u, 44883u, 44885u, 44884u, 44887u, 44888u, 44889u, 47093u, 44891u, 44890u,
			44895u, 44886u, 44881u, 44892u, 44894u, 44893u, 44905u, 44904u, 44880u, 46751u,
			46750u
		};
		foreach(uint id in array) {
			Requests.AddAction(id);
		}
		Requests.Executed+=(uint action,ulong target) => {
			if(DalamudApi.ObjectTable.FirstOrDefault((IGameObject o) => o.GameObjectId==target) is IBattleChara target2) {
				rotation.NotifyActionUsed(action,target2);
			}
		};
		DalamudApi.Framework.Update+=OnFrameworkUpdate;
	}
	public void Dispose() {
		DalamudApi.Framework.Update-=OnFrameworkUpdate;
		Requests.Dispose();
		CrucibleAutoPreset.Sync(bossMod,wantActive: false);
	}
	public float FamiliarHealthOf(int number) {
		return rotation.FamiliarHealthOf(number);
	}
	private static bool ReactionHelperIsTargeting() {
		try {
			return DalamudApi.PluginInterface.GetIpcSubscriber<bool>("ReactionHelper.TargetingActive").InvokeFunc();
		}
		catch {
			return false;
		}
	}
	public void RequestSummon(int slot) {
		if(slot>=1&&slot<=3) {
			pendingSummonSlot=slot;
			pendingSummonExpiresUtc=DateTime.UtcNow.AddSeconds(5.0);
		}
	}
	public void SetEnabled(bool enabled) {
		configuration.RotationEnabled=enabled;
		configuration.Save();
		if(!enabled) {
			BeastmasterActionRequest.Clear();
			rotation.Reset();
			rotation.UpdatePetStance(null);
			Running=false;
			StatusText="Rotation off";
			NextActionName="-";
			NextActionReason="-";
			Phase="Idle";
		}
	}
	private unsafe void OnFrameworkUpdate(IFramework framework) {
		Requests.Block("Waiting for rotation timing or character availability");
		DateTime utcNow=DateTime.UtcNow;
		if(utcNow<nextTickUtc) {
			return;
		}
		nextTickUtc=utcNow.AddMilliseconds(Math.Clamp(configuration.UpdateIntervalMs,0,1000));
		PlayerMovement.Update();
		if(utcNow>=nextCoveredDropUtc&&!RotationToggleOverrides.Get("KeepCovered",baseline: true)&&!RotationToggleOverrides.Get("KeepCovered",baseline: false)&&BeastmasterDutyActions.IsCovered()) {
			nextCoveredDropUtc=utcNow.AddSeconds(1.0);
			BeastmasterDutyActions.DropCovered();
		}
		BeastmasterCaptureAttempts.NoteTerritory((ushort)DalamudApi.ClientState.TerritoryType);
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer!=null&&localPlayer.ClassJob.RowId==43) {
			rotation.ObserveFamiliar(BeastmasterGaugeSnapshot.Read());
			uint territoryType=DalamudApi.ClientState.TerritoryType;
			bool flag=territoryType-1339<=4;
			bool flag2=flag;
			int num;
			if(flag2&&configuration.CrucibleAutoConfigureBmrMovement) {
				num=(bossMod.InCrucibleEncounter?1:0);
				if(num!=0) {
					moduleLastSeenUtc=utcNow;
				}
			}
			else {
				num=0;
			}
			bool wantActive=num!=0||(flag2&&configuration.CrucibleAutoConfigureBmrMovement&&CrucibleAutoPreset.IsActive&&(utcNow-moduleLastSeenUtc).TotalSeconds<3.0);
			CrucibleAutoPreset.Sync(bossMod,wantActive);
		}
		if(RotationToggleOverrides.Get("AiAssist",configuration.RotationEnabled)||pendingSummonSlot!=0) {
			IPlayerCharacter? localPlayer2=DalamudApi.ObjectTable.LocalPlayer;
			if(localPlayer2!=null&&localPlayer2.ClassJob.RowId==43&&bossMod.InCrucibleEncounter&&bossMod.ShouldHoldActions) {
				Requests.Block("BossMod mechanic hold");
				if(bossMod.IsFacingRestricted) {
					BeastmasterAutoAttack.Stop();
				}
				OutOfRange=false;
				Set("Waiting - BossMod is resolving a mechanic","-","Movement, facing or cast cancellation is active","Mechanic hold");
				SetCycleStatus(rotation,"BossMod mechanic hold","Actions resume automatically when the mechanic hold clears.");
				return;
			}
		}
		if(pendingSummonSlot!=0) {
			TryPendingSummon();
		}
		if(!RotationToggleOverrides.Get("AiAssist",configuration.RotationEnabled)) {
			Running=false;
			StatusText="Rotation off";
			NextActionName="-";
			NextActionReason="Enable the rotation to start";
			Phase="Idle";
			SetCycleStatus(rotation,"AI Assist off","Nothing runs the cycle while AI Assist is switched off.");
			return;
		}
		IPlayerCharacter player=DalamudApi.ObjectTable.LocalPlayer;
		if(player==null) {
			Set("Waiting for the character to load","-","-","Idle");
			SetCycleStatus(rotation,"Loading","Waiting for the character to finish loading.");
			return;
		}
		if(player.ClassJob.RowId!=43) {
			Set("Switch to Beastmaster","-","The rotation only runs on Beastmaster","Idle");
			SetCycleStatus(rotation,"Wrong job","The rotation and its familiar cycle only run on Beastmaster.");
			return;
		}
		rotation.UpdatePetStance(null);
		if(DalamudApi.Condition[ConditionFlag.BetweenAreas]||DalamudApi.Condition[ConditionFlag.Mounted]||DalamudApi.Condition[ConditionFlag.OccupiedInCutSceneEvent]||player.CurrentHp==0||player.IsCasting) {
			Set("Waiting until actions can be used","-","The character cannot act right now","Idle");
			SetCycleStatus(rotation,"Cannot act","Mounted, in a cutscene, between areas or otherwise unable to use actions.");
			return;
		}
		if(utcNow>=nextActionUtc&&Requests.TryExecute((ulong id) => {
			if(id!=player.GameObjectId) {
				IGameObject gameObject=DalamudApi.ObjectTable.FirstOrDefault((IGameObject o) => o.GameObjectId==id);
				if(gameObject is IBattleChara battleChara3&&!gameObject.IsDead) {
					return battleChara3.CurrentHp!=0;
				}
				return false;
			}
			return true;
		},!DalamudApi.Condition[ConditionFlag.InCombat]||DalamudApi.TargetManager.Target==null)) {
			nextActionUtc=utcNow.AddMilliseconds(700.0);
			return;
		}
		ulong preferredTarget=bossMod.PreferredTargetID;
		ulong num2=((BeastmasterKinship.Borrowed()==BeastKin.Wavekin&&RotationToggleOverrides.Get("BeastMode",configuration.RotationUseBeastMode))?bossMod.DispelTargetID:0);
		if(num2!=0L) {
			preferredTarget=num2;
		}
		bool flag3=CrucibleFightPlan.HandedToReactionHelper(configuration,bossMod.EncounterName)||ReactionHelperIsTargeting();
		if(flag3&&num2==0L) {
			preferredTarget=0uL;
		}
		if(bossMod.InCrucibleEncounter&&preferredTarget!=0L) {
			IGameObject? target=DalamudApi.TargetManager.Target;
			if((target==null||target.GameObjectId!=preferredTarget)&&DalamudApi.ObjectTable.FirstOrDefault((IGameObject o) => o.GameObjectId==preferredTarget) is IBattleChara { IsTargetable: not false,IsDead: false,CurrentHp: >0u } battleChara) {
				DalamudApi.TargetManager.Target=battleChara;
				goto IL_0537;
			}
		}
		if(num2==0L&&!flag3&&bossMod.ShouldClearTarget) {
			DalamudApi.TargetManager.Target=null;
		}
		goto IL_0537;
		IL_06b9:
		BeastmasterRotationRunner beastmasterRotationRunner=this;
		RotationDecision rotationDecision;
		string action=(rotationDecision.HasAction?BeastmasterGameData.ActionName(rotationDecision.ActionId,"Action"):"-");
		string reason;
		if(rotationDecision.HasAction) {
			reason=rotationDecision.Reason;
		}
		else {
			reason=((BeastmasterActionRequest.Pending!=0)?rotation.LastReason:"No valid target selected");
		}
		beastmasterRotationRunner.Set("Waiting for a hostile target",action,reason,"Idle");
		if(DalamudApi.Condition[ConditionFlag.InCombat]) {
			SetCycleStatus(rotation,"No target","Select a hostile target for the rotation to work on.");
		}
		else {
			SetCycleStatus(rotation,"Waiting for combat","Parting Blow can only be used in combat, so the cycle cannot run out of a fight.");
		}
		return;
		IL_0537:
		AddBurstTiming.Update();
		IBattleChara target2=AddBurstTiming.Target;
		if(target2!=null&&DalamudApi.TargetManager.Target?.GameObjectId!=target2.GameObjectId) {
			DalamudApi.TargetManager.Target=target2;
		}
		if(DalamudApi.TargetManager.Target is IBattleChara { ObjectKind: ObjectKind.BattleNpc,IsTargetable: not false,IsDead: false,CurrentHp: not 0u } battleChara2) {
			BeastmasterGaugeSnapshot gauge=BeastmasterGaugeSnapshot.Read();
			RotationDecision rotationDecision2=rotation.Decide(player,battleChara2,gauge);
			CycleStatus=rotation.CycleStatus;
			CycleDetail=rotation.CycleDetail;
			EnemiesNearTarget=rotation.EnemiesNearTarget;
			Phase=rotationDecision2.Phase;
			NextActionReason=rotationDecision2.Reason;
			NextActionName=(rotationDecision2.HasAction?BeastmasterGameData.ActionName(rotationDecision2.ActionId,$"Action {rotationDecision2.ActionId}"):"-");
			if(!rotationDecision2.HasAction) {
				Running=true;
				StatusText="Holding";
				return;
			}
			if(!BeastmasterActionRanges.IsSelfCast(rotationDecision2.ActionId)) {
				float num3=BeastmasterActionRanges.Of(rotationDecision2.ActionId);
				float num4=BeastmasterActionRanges.EdgeDistance(player,battleChara2);
				if(num4>num3) {
					Running=true;
					OutOfRange=true;
					StatusText=(bossMod.IsDodging?$"Waiting - {NextActionName} needs {num3:0}y, target is {num4:0.#}y away while dodging":$"Waiting - {NextActionName} needs {num3:0}y, target is {num4:0.#}y away");
					return;
				}
			}
			OutOfRange=false;
			if(utcNow<nextActionUtc) {
				Running=true;
				StatusText="Queued: "+NextActionName;
				return;
			}
			ActionManager* ptr=ActionManager.Instance();
			if(ptr==null) {
				Set("ActionManager unavailable","-","Cannot reach the action system","Blocked");
				return;
			}
			ulong targetId=BeastmasterRotationService.SelfOr(battleChara2.GameObjectId,rotationDecision2.ActionId);
			bool flag=rotationDecision2.ActionId==44886;
			if(flag) {
				BeastKin beastKin=BeastmasterKinship.Borrowed();
				bool flag4=((beastKin==BeastKin.Wavekin||beastKin==BeastKin.Soulkin)?true:false);
				flag=flag4;
			}
			if(flag) {
				targetId=battleChara2.GameObjectId;
			}
			bool flag5=BeastmasterDutyActions.IsDutyAction(rotationDecision2.ActionId);
			uint num5=rotationDecision2.ActionId;
			if(!flag5) {
				uint num6=ptr->GetActionStatus(ActionType.Action,num5,targetId,checkRecastActive: true,checkCastingActive: true,null);
				if(num6!=0&&rotationDecision2.ActionId==44886) {
					uint adjustedActionId=ptr->GetAdjustedActionId(44886u);
					if(adjustedActionId!=44886&&ptr->GetActionStatus(ActionType.Action,adjustedActionId,targetId,checkRecastActive: true,checkCastingActive: true,null)==0) {
						num5=adjustedActionId;
						num6=0u;
					}
				}
				if(num6!=0) {
					nextActionUtc=utcNow.AddMilliseconds(500.0);
					StatusText="Waiting - "+NextActionName+" target is no longer valid";
					Running=true;
					return;
				}
			}
			if(flag5) {
				rotation.NoteDutyActionAttempt();
			}
			bool flag6=(flag5?BeastmasterDutyActions.Execute(rotationDecision2.ActionId):ptr->UseAction(ActionType.Action,num5,targetId,0u,ActionManager.UseActionMode.None,0u,null));
			if(rotationDecision2.ActionId==44886) {
				uint adjustedActionId2=ptr->GetAdjustedActionId(44886u);
				if(!flag6&&num5==44886&&adjustedActionId2!=44886) {
					flag6=ptr->UseAction(ActionType.Action,adjustedActionId2,targetId,0u,ActionManager.UseActionMode.None,0u,null);
				}
				DalamudApi.Log.Information("Beast Mode ({Reason}): pressed id {Id} (swapped action {Swapped}) -> {Result}.",rotationDecision2.Reason,44886u,adjustedActionId2,flag6?"went":"refused");
			}
			if(rotationDecision2.ActionId==44890||rotationDecision2.ActionId==44895) {
				uint adjustedActionId3=ptr->GetAdjustedActionId(rotationDecision2.ActionId);
				if(!flag6&&adjustedActionId3!=rotationDecision2.ActionId) {
					flag6=ptr->UseAction(ActionType.Action,adjustedActionId3,battleChara2.GameObjectId,0u,ActionManager.UseActionMode.None,0u,null);
					DalamudApi.Log.Information("{Action} refused; pressed its swapped action {Adjusted} at the target -> {Result}.",NextActionName,adjustedActionId3,flag6?"went":"refused");
				}
				DalamudApi.Log.Information("{Action} ({Reason}): {Result}.",NextActionName,rotationDecision2.Reason,flag6?"went":"refused");
			}
			if(flag6) {
				nextActionUtc=utcNow.AddMilliseconds(300.0);
				StatusText="Used "+NextActionName;
				rotation.NotifyActionUsed(rotationDecision2.ActionId,battleChara2);
			}
			else {
				nextActionUtc=utcNow.AddMilliseconds(flag5?1000:500);
				StatusText="Could not use "+NextActionName;
			}
			Running=true;
			return;
		}
		rotation.Reset();
		rotationDecision=rotation.DecideWithoutTarget(player,BeastmasterGaugeSnapshot.Read());
		if(rotationDecision.HasAction&&utcNow>=nextActionUtc) {
			ActionManager* ptr2=ActionManager.Instance();
			bool num7;
			if(!BeastmasterDutyActions.IsDutyAction(rotationDecision.ActionId)) {
				if(ptr2==null) {
					goto IL_06b9;
				}
				num7=ptr2->UseAction(ActionType.Action,rotationDecision.ActionId,player.GameObjectId,0u,ActionManager.UseActionMode.None,0u,null);
			}
			else {
				num7=BeastmasterDutyActions.Execute(rotationDecision.ActionId);
			}
			if(num7) {
				nextActionUtc=utcNow.AddMilliseconds(1500.0);
				BeastmasterActionRequest.NoteUsed(rotationDecision.ActionId);
				string text=BeastmasterGameData.ActionName(rotationDecision.ActionId,"Action");
				Set("Used "+text,text,rotationDecision.Reason,rotationDecision.Phase);
				return;
			}
		}
		goto IL_06b9;
	}
	private unsafe void TryPendingSummon() {
		if(PlayerMovement.IsMoving&&DateTime.UtcNow<pendingSummonExpiresUtc) {
			StatusText="Waiting until you stop moving to summon";
			return;
		}
		int num=pendingSummonSlot;
		pendingSummonSlot=0;
		try {
			IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
			if(localPlayer==null||localPlayer.ClassJob.RowId!=43) {
				return;
			}
			uint actionId=BeastmasterActions.Battlehorns[num-1];
			if(!BeastmasterActions.IsUnlocked(actionId,localPlayer.Level)) {
				DalamudApi.ChatGui.Print($"[Beast Master Helper] Battlehorn {num} unlocks at level {BeastmasterActions.UnlockLevel(actionId)}.");
				return;
			}
			ActionManager* ptr=ActionManager.Instance();
			if(ptr!=null) {
				if(ptr->GetActionStatus(ActionType.Action,actionId,localPlayer.GameObjectId,checkRecastActive: true,checkCastingActive: true,null)!=0) {
					StatusText=$"Battlehorn {num} is not ready";
				}
				else if(ptr->UseAction(ActionType.Action,actionId,localPlayer.GameObjectId,0u,ActionManager.UseActionMode.None,0u,null)) {
					StatusText=$"Summoning from Battlehorn {num}";
					BeastmasterActionRequest.NoteUsed(actionId);
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not summon from Battlehorn {Slot}.",num);
		}
	}
	private void SetCycleStatus(BeastmasterRotationService service,string status,string detail) {
		service.NoteCycleIdle(status,detail);
		CycleStatus=service.CycleStatus;
		CycleDetail=service.CycleDetail;
	}
	private void Set(string status,string action,string reason,string phase) {
		Running=false;
		StatusText=status;
		NextActionName=action;
		NextActionReason=reason;
		Phase=phase;
	}
}
