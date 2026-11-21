using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
namespace Beastmaster;

public sealed class BeastmasterRotationService {
	private readonly BeastmasterConfiguration configuration;
	private readonly BeastmasterProgressService? progressService;
	private int openerStep;
	private DateTime familiarSummonedAt=DateTime.MinValue;
	private uint lastSeenSummon;
	private int lastSummonedSlot=-1;
	private readonly Dictionary<int,float> lastKnownFamiliarHp=new Dictionary<int,float>();
	private DateTime rallyWaitingSince=DateTime.MinValue;
	private DateTime rallyUniversalityWaitingSince=DateTime.MinValue;
	private DateTime axePoolWaitingSince=DateTime.MinValue;
	private DateTime finisherPendingUntil=DateTime.MinValue;
	private DateTime cheerWaitingSince=DateTime.MinValue;
	private int swapAttempts;
	private int swapAttemptsForSlot;
	private uint swapAttemptsForSummon;
	private const int MaxSwapAttempts=3;
	private bool openerActive;
	private bool wasInCombat;
	private readonly BeastmasterTrickWindow trickWindow=new BeastmasterTrickWindow();
	private bool waitingForTrick;
	private readonly BossModBridge bossMod;
	private const float ChargeAoeRadius=6f;
	private (uint,string) captureSkipLogged;
	private DateTime dutyActionSettlesAt=DateTime.MinValue;
	private DateTime beastModeNotReadyLoggedAt=DateTime.MinValue;
	private bool petSteadyed;
	private DateTime petSteadyTriedAt=DateTime.MinValue;
	private bool morphoEpisode;
	private ulong steadiedSummon;
	private bool drakeSeenAlive;
	private DateTime? phaseGapSince;
	private DateTime? morphoRestrictSince;
	private DateTime phaseLoggedAt=DateTime.MinValue;
	private bool morphosSeen;
	private BeastKin lastLoggedKin;
	private const float UniversalityHoldSeconds=60f;
	private DateTime? _meteorHoldSince;
	private const double MeteorHoldSeconds=20.0;
	private const float KinshipRefreshSeconds=32f;
	private float targetHpPercent=100f;
	private DateTime potionTriedAt=DateTime.MinValue;
	private bool burnTargetIsLast;
	private DateTime natureNotReadyLoggedAt=DateTime.MinValue;
	private DateTime temperedForAddsAt=DateTime.MinValue;
	private DateTime meteorLoggedAt=DateTime.MinValue;
	private bool natureWasUp;
	private DateTime molesGatheredSince=DateTime.MinValue;
	private DateTime meteorDiagAt=DateTime.MinValue;
	private DateTime refreshSkipLoggedAt=DateTime.MinValue;
	private bool holdSummonForOpener;
	private DateTime? openerHoldSince;
	private const uint EnmityUpStatus=5586u;
	private const double NearDeathPercent=12.0;
	private int lastFamiliarHpPercent=-1;
	private DateTime lastPartingBlowAt=DateTime.MinValue;
	private int planSwapTries;
	private ulong planSwapFor;
	private const float MeleeRange=3.5f;
	private const float QuellingWaveRange=30f;
	private DateTime summonGuardUntil=DateTime.MinValue;
	private const double SummonGuardSeconds=5.0;
	private DateTime lastTrickDecidedAt=DateTime.MinValue;
	private BeastmasterGaugeSnapshot? traceGauge;
	private int traceLevel;
	private string lastTraceLine=string.Empty;
	private DateTime lastTraceAt=DateTime.MinValue;
	public int UniversalitySession { get; private set; }
	public string LastRallyNote { get; private set; }=string.Empty;
	public DateTime LastRallyAt { get; private set; }=DateTime.MinValue;
	public string Phase { get; private set; }="Idle";
	public string LastReason { get; private set; }="-";
	public int EnemiesNearTarget { get; private set; }
	public InstinctAffinity HeldHeart { get; private set; }
	public InstinctAffinity WantedAffinity { get; private set; }
	public uint HeldInfinitiveHalf { get; private set; }
	public bool InfinitiveReady { get; private set; }
	public string GapCloseStatus { get; private set; }="Not evaluated";
	public BeastKin BorrowedKinship => BeastmasterKinship.Borrowed();
	public string CycleStatus { get; private set; }="Not evaluated";
	public string CycleDetail { get; private set; }="The cycle has not been evaluated yet.";
	private bool DutyActionSettling => DateTime.UtcNow<dutyActionSettlesAt;
	private bool Burning {
		get {
			if(!RotationToggleOverrides.Get("Burn",configuration.RotationBurn)&&!AddBurstTiming.Burst) {
				return BelowBurnHp;
			}
			return true;
		}
	}
	private bool BelowBurnHp {
		get {
			if(configuration.RotationBurnBelowEnemyHpPercent>0f) {
				return burnTargetIsLast;
			}
			return false;
		}
	}
	public string CoveredStatus { get; private set; }="Not evaluated";
	public static bool AnyFamiliarOut => FamiliarOut()!=null;
	public bool SummonGuardActive => DateTime.UtcNow<summonGuardUntil;
	public float FamiliarHealthOf(int number) {
		if(!lastKnownFamiliarHp.TryGetValue(number,out var value)) {
			return 100f;
		}
		return value;
	}
	private bool UniversalityWindow(int level) {
		if(level>=50) {
			return HeldInfinitiveHalf!=0;
		}
		return false;
	}
	public BeastmasterRotationService(BeastmasterConfiguration configuration,BeastmasterProgressService? progressService=null,BossModBridge? bossMod=null) {
		this.bossMod=bossMod ?? new BossModBridge(DalamudApi.PluginInterface);
		CrucibleFightPlan.Bridge=this.bossMod;
		this.configuration=configuration;
		this.progressService=progressService;
	}
	public unsafe RotationDecision DecideWithoutTarget(IBattleChara player,BeastmasterGaugeSnapshot gauge) {
		StopAutoAttackIfOff();
		HandlePotionRequest();
		ActionManager* ptr=ActionManager.Instance();
		if(ptr==null) {
			return RotationDecision.None;
		}
		RotationDecision result=DecideRequested(player,ptr);
		if(result.HasAction) {
			return result;
		}
		if(gauge.SummonDataId!=0&&MorphoPhase()&&HasOwnStatus(4601u)) {
			BeastmasterCatalogEntry summonEntry=gauge.SummonEntry;
			if((object)summonEntry!=null&&CrucibleFightPlan.NatureChoice(configuration,summonEntry.Number)==PlannedUse.Borrow&&BeastmasterActions.IsUnlocked(44895u,player.Level)&&Ready(ptr,44895u,player.GameObjectId)) {
				return Decide("Borrow: renewing the kinship during the Morpho phase - the familiar stays out on Steady","Cooldown",44895u);
			}
		}
		if(!AutoSummonAllowed()||gauge.SummonDataId!=0||(MorphoPhase()&&!MorphoRefreshSummonAllowed())) {
			return RotationDecision.None;
		}
		if(PlayerMovement.IsMoving) {
			LastReason="Holding the summon until you stop - Battlehorns have a 1s cast";
			return RotationDecision.None;
		}
		uint num=ChooseBattlehorn(player.Level,ptr,player.GameObjectId);
		if(num!=0) {
			return new RotationDecision(num,LastReason,"Summon");
		}
		return RotationDecision.None;
	}
	private unsafe RotationDecision DecideRequested(IBattleChara player,ActionManager* actionManager) {
		uint pending=BeastmasterActionRequest.Pending;
		if(pending==0||pending==900001) {
			return RotationDecision.None;
		}
		string text=BeastmasterGameData.ActionName(pending,"That action");
		if(!BeastmasterDutyActions.IsDutyAction(pending)&&!BeastmasterActions.IsUnlocked(pending,player.Level)) {
			LastReason=$"{text} unlocks at level {BeastmasterActions.UnlockLevel(pending)}";
			return RotationDecision.None;
		}
		if(!BeastmasterActions.IsSelfTargeted(pending)&&!BeastmasterDutyActions.IsDutyAction(pending)) {
			LastReason=text+" needs a target - select something first";
			return RotationDecision.None;
		}
		if(BeastmasterActions.HasCastTime(pending)&&PlayerMovement.IsMoving) {
			LastReason=text+" has a 1s cast - waiting until you stop";
			return RotationDecision.None;
		}
		if(!Ready(actionManager,pending,player.GameObjectId)) {
			LastReason=text+" is not usable yet - still requested";
			return RotationDecision.None;
		}
		LastReason=text+": you asked for this one";
		return new RotationDecision(pending,LastReason,"Requested");
	}
	public void Reset() {
		openerStep=0;
		openerActive=false;
		trickWindow.Reset();
		waitingForTrick=false;
		Phase="Idle";
		lastKnownFamiliarHp.Clear();
	}
	public unsafe RotationDecision Decide(IBattleChara player,IBattleChara target,BeastmasterGaugeSnapshot gauge) {
		byte level=player.Level;
		bool flag=DalamudApi.Condition[ConditionFlag.InCombat];
		traceGauge=gauge;
		traceLevel=level;
		targetHpPercent=((target.MaxHp!=0)?((float)target.CurrentHp*100f/(float)target.MaxHp):100f);
		burnTargetIsLast=targetHpPercent<configuration.RotationBurnBelowEnemyHpPercent&&!OtherEnemyStillHealthy(target);
		StopAutoAttackIfOff();
		HandlePotionRequest();
		if(flag&&!wasInCombat) {
			openerActive=RotationToggleOverrides.Get("Opener",configuration.RotationUseOpener);
			openerStep=0;
		}
		else if(!flag) {
			openerActive=false;
			openerStep=0;
		}
		wasInCombat=flag;
		TrackSummonChanges(gauge);
		if(gauge.SummonDataId==0) {
			if(temperedForAddsAt!=DateTime.MinValue&&!morphoRestrictSince.HasValue&&CrucibleFightPlan.Applies(configuration)) {
				morphoRestrictSince=DateTime.UtcNow;
				DalamudApi.Log.Information("Meteor and Parting Blow done: no familiar is summoned until all the Morphos are fed.");
			}
			temperedForAddsAt=DateTime.MinValue;
		}
		bool flag2=HasOwnStatus(4601u);
		if(natureWasUp&&!flag2) {
			BeastmasterCatalogEntry summonEntry=gauge.SummonEntry;
			if((object)summonEntry!=null&&summonEntry.Number==50&&CrucibleFightPlan.Applies(configuration)&&CrucibleFightPlan.Active.TemperedFor.Length!=0&&temperedForAddsAt==DateTime.MinValue) {
				temperedForAddsAt=DateTime.UtcNow;
				DateTime valueOrDefault=morphoRestrictSince.GetValueOrDefault();
				if(!morphoRestrictSince.HasValue) {
					valueOrDefault=DateTime.UtcNow;
					morphoRestrictSince=valueOrDefault;
				}
				DalamudApi.Log.Information("Behemoth's Tempered Release spent - Parting Blow follows once Meteor has landed.");
			}
		}
		natureWasUp=flag2;
		BeastKin borrowedKinship=BorrowedKinship;
		if(borrowedKinship!=lastLoggedKin) {
			DalamudApi.Log.Information("Kinship lent: {Was} -> {Now} (familiar out: {Familiar}, last press: {Last}).",lastLoggedKin,borrowedKinship,gauge.SummonEntry?.DisplayName ?? "none",LastReason);
			lastLoggedKin=borrowedKinship;
		}
		if(finisherPendingUntil>DateTime.UtcNow&&gauge.Tp<200) {
			finisherPendingUntil=DateTime.MinValue;
			UniversalitySession++;
			configuration.UniversalityCombosLanded++;
			configuration.Save();
			DalamudApi.Log.Information("Universality landed (session {Session}, total {Total}).",UniversalitySession,configuration.UniversalityCombosLanded);
		}
		else if(finisherPendingUntil!=DateTime.MinValue&&finisherPendingUntil<=DateTime.UtcNow) {
			finisherPendingUntil=DateTime.MinValue;
		}
		HeldHeart=ReadHeldHeart();
		waitingForTrick=trickWindow.Waiting(DateTime.UtcNow,HeldHeart!=InstinctAffinity.None,gauge.SummonDataId!=0);
		WantedAffinity=BeastmasterActions.NextInWheel(HeldHeart);
		HeldInfinitiveHalf=BeastmasterStatusReader.InfinitiveHalf();
		InfinitiveReady=HeldInfinitiveHalf!=0&&BeastmasterActions.IntentionalHalf(WantedAffinity)==BeastmasterActions.OppositeHalf(HeldInfinitiveHalf);
		EnemiesNearTarget=CountEnemiesNear(target);
		ActionManager* ptr=ActionManager.Instance();
		if(ptr==null) {
			return Decide("ActionManager unavailable","Blocked",0u);
		}
		uint pending=BeastmasterActionRequest.Pending;
		if(pending!=0&&(BeastmasterDutyActions.IsDutyAction(pending)||BeastmasterActions.IsUnlocked(pending,level))&&Ready(ptr,pending,target)) {
			return Decide(BeastmasterGameData.ActionName(pending,"Requested action")+": you asked for this one","Requested",pending);
		}
		if(AutoSummonAllowed()&&gauge.SummonDataId==0&&!PlayerMovement.IsMoving&&(!MorphoPhase()||MorphoRefreshSummonAllowed())) {
			uint num=ChooseBattlehorn(level,ptr,target.GameObjectId);
			if(num!=0) {
				return Decide("No familiar out - summoning","Summon",num);
			}
		}
		if(PlayerMovement.IsMoving&&gauge.SummonDataId==0&&AutoSummonAllowed()) {
			LastReason="Holding the summon until you stop - Battlehorns have a 1s cast";
		}
		RotationDecision result=((MorphoPhase()&&!PostMeteorDue())?RotationDecision.None:NextCycleDismiss(level,gauge,ptr,target));
		if(result.HasAction) {
			return result;
		}
		RotationDecision result2=NextDutyEnmity(level,gauge,ptr,target);
		if(result2.HasAction) {
			return result2;
		}
		RotationDecision result3=SwapForPlan(level,gauge,ptr,target);
		if(result3.HasAction) {
			return result3;
		}
		RotationDecision result4=SwapToPinnedFamiliar(level,gauge,ptr,target);
		if(result4.HasAction) {
			return result4;
		}
		if(bossMod.DispelTargetID!=0L&&bossMod.DispelTargetID==target.GameObjectId) {
			RotationDecision result5=((BorrowedKinship==BeastKin.Wavekin)?NextBeastMode(level,ptr,target,DistanceToTarget(target)>3.5f):RotationDecision.None);
			if(result5.HasAction) {
				return result5;
			}
			LastReason="Dispel target: Quelling Wave only - attacking it would reflect the damage";
			return RotationDecision.None;
		}
		if(CrucibleFightPlan.Applies(configuration)&&IsMorpho(target)&&!bossMod.PreferChallenge&&BorrowedKinship==BeastKin.Wavekin) {
			RotationDecision result6=NextBeastMode(level,ptr,target,DistanceToTarget(target)>3.5f);
			if(result6.HasAction) {
				return result6;
			}
			RotationDecision result7=NextOneWithNature(level,gauge,ptr,target);
			if(result7.HasAction) {
				return result7;
			}
			LastReason="Morpho: Quelling Wave only, waiting for it to recharge - no melee that could kill it";
			return RotationDecision.None;
		}
		RotationDecision result8=NextShieldCharge(level,ptr,target);
		if(result8.HasAction) {
			return result8;
		}
		bool outOfRange=DistanceToTarget(target)>3.5f;
		RotationDecision result9=NextCapture(level,ptr,target);
		if(result9.HasAction) {
			return result9;
		}
		if(openerActive) {
			RotationDecision result10=NextOpenerStep(level,gauge,ptr,target);
			if(result10.HasAction) {
				return result10;
			}
			openerActive=false;
		}
		if(CrucibleFightPlan.Applies(configuration)) {
			BeastmasterCatalogEntry summonEntry=gauge.SummonEntry;
			if((object)summonEntry!=null&&summonEntry.Number==50&&CrucibleFightPlan.Active.TemperedFor.Length!=0&&(DateTime.UtcNow-meteorDiagAt).TotalSeconds>=3.0) {
				meteorDiagAt=DateTime.UtcNow;
				DalamudApi.Log.Information("Meteor state: cooldowns {Cd}, nature up {Nature}, tempered ready {Ready}, adds alive {Adds}, dispel pending {Dispel}, wave lent {Wave}, target {Target}.",RotationToggleOverrides.Get("Cooldowns",configuration.RotationUseCooldowns),HasOwnStatus(4601u),Ready(ptr,44890u,target),CountNamedNear(CrucibleFightPlan.Active.TemperedFor,target.Position,1000f),CrucibleFightPlan.DispelPending(configuration),BorrowedKinship==BeastKin.Wavekin,target.Name.TextValue);
			}
		}
		if(RotationToggleOverrides.Get("Cooldowns",configuration.RotationUseCooldowns)&&CrucibleFightPlan.Applies(configuration)) {
			BeastmasterCatalogEntry summonEntry=gauge.SummonEntry;
			if((object)summonEntry!=null&&summonEntry.Number==50) {
				RotationDecision result11=NextOneWithNature(level,gauge,ptr,target);
				if(meteorDiagAt==DateTime.UtcNow||(DateTime.UtcNow-meteorDiagAt).TotalMilliseconds<200.0) {
					DalamudApi.Log.Information("Meteor decision: {Has} - {Reason}.",result11.HasAction,LastReason);
				}
				if(result11.HasAction) {
					return result11;
				}
			}
		}
		bool hasAction=NextComboStep(level,ptr,target,outOfRange).HasAction;
		bool stacksLookSane;
		int num2;
		bool flag3;
		int num4;
		if(RotationToggleOverrides.Get("Cooldowns",configuration.RotationUseCooldowns)&&!hasAction) {
			stacksLookSane=gauge.StacksLookSane;
			num2=((Burning||!stacksLookSane||level<50)?1:configuration.RotationRallyMinStacks);
			flag3=level>=50&&!Burning;
			if(RotationToggleOverrides.Get("Rally",configuration.RotationUseRally)&&BeastmasterActions.IsUnlocked(44905u,level)&&!AddBurstTiming.Holding) {
				bool num3;
				if(!flag3) {
					num3=gauge.Tp<(UniversalityWindow(level)?250:150);
				}
				else {
					if(!UniversalityWindow(level)) {
						goto IL_091d;
					}
					num3=gauge.Tp<250;
				}
				if(num3&&Ready(ptr,44905u,target)) {
					num4=(OffCooldown(ptr,44905u)?1:0);
					goto IL_091e;
				}
			}
			goto IL_091d;
		}
		goto IL_0f6a;
		IL_0dc5:
		int num5=((Burning||!stacksLookSane)?1:configuration.RotationCheerMinStacks);
		bool flag4=RotationToggleOverrides.Get("RallyingCheer",configuration.RotationUseRallyingCheer)&&BeastmasterActions.IsUnlocked(44904u,level)&&!AddBurstTiming.Holding&&gauge.BeastPower<150&&Ready(ptr,44904u,target)&&OffCooldown(ptr,44904u);
		bool flag5=gauge.BeastSoulStacks>=num5||!stacksLookSane;
		bool flag6=Stalled(ref cheerWaitingSince,flag4&&!flag5&&gauge.BeastSoulStacks>0);
		if(flag4&&(flag5|flag6)) {
			cheerWaitingSince=DateTime.MinValue;
			int value=Math.Min(240,30+gauge.BeastSoulStacks*70);
			return Decide(flag5?$"Rallying Cheer: {gauge.BeastSoulStacks} stack(s) -> {value} familiar TP":$"Rallying Cheer at {gauge.BeastSoulStacks} stack(s) -> {value} familiar TP - ready too long to keep holding","Cooldown",44904u);
		}
		RotationDecision result12=NextOneWithNature(level,gauge,ptr,target);
		if(result12.HasAction) {
			return result12;
		}
		goto IL_0f6a;
		IL_0f6a:
		if(RotationToggleOverrides.Get("Aoe",configuration.RotationUseAoe)&&EnemiesNearTarget>=configuration.RotationAoeThreshold) {
			RotationDecision result13=NextAoeAction(level,gauge,ptr,target);
			if(result13.HasAction) {
				return result13;
			}
		}
		RotationDecision result14=NextBeastMode(level,ptr,target,outOfRange);
		if(result14.HasAction) {
			return result14;
		}
		if(RotationToggleOverrides.Get("PowerActions",configuration.RotationUsePowerActions)&&RotationToggleOverrides.Get("Trick",baseline: true)&&BeastmasterActions.IsUnlocked(47093u,level)&&gauge.BeastPower>=100&&!waitingForTrick&&HeldHeart==InstinctAffinity.None&&!HasOwnStatus(4643u)&&Ready(ptr,47093u,target)) {
			if(!RotationToggleOverrides.Get("TrickWaitsTp",configuration.RotationTrickWaitForTp)||!AnyAxeUnlocked(level)||gauge.Tp>=100) {
				return Decide($"Trick: familiar TP {gauge.BeastPower} - grants a Heart","Wheel",47093u);
			}
			LastReason=$"Holding Trick until TP reaches 100 for the axe to follow it (TP {gauge.Tp})";
		}
		RotationDecision result15=NextAxe(level,gauge,ptr,target);
		if(result15.HasAction) {
			return result15;
		}
		return NextComboStep(level,ptr,target,outOfRange);
		IL_091d:
		num4=0;
		goto IL_091e;
		IL_091e:
		bool flag7=(byte)num4!=0;
		bool flag8=gauge.BeastHeartStacks>=num2||!stacksLookSane;
		bool flag9=!flag3&&Stalled(ref rallyWaitingSince,flag7&&!flag8&&gauge.BeastHeartStacks>0);
		bool flag10=((level>=50&&configuration.RotationUniversalityHoldSeconds>0)&flag7)&&(flag8|flag9)&&!UniversalityWindow(level)&&!Burning;
		bool flag11=Stalled(ref rallyUniversalityWaitingSince,flag10,configuration.RotationUniversalityHoldSeconds);
		if(flag10&&!flag11) {
			LastReason=$"Holding Rally for Universality: waiting for a banked half and a Heart (TP {gauge.Tp})";
		}
		bool flag12=(UniversalityWindow(level)&stacksLookSane)&&gauge.Tp+40+gauge.BeastHeartStacks*70<250;
		if(flag7&flag12) {
			LastReason=$"Not Rally in this window: {gauge.BeastHeartStacks} stack(s) + {gauge.Tp} TP would only reach {Math.Min(250,gauge.Tp+40+gauge.BeastHeartStacks*70)}, short of the 250 the finisher needs";
		}
		if(flag7&&!flag12&&(flag8|flag9)&&(!flag10||flag11)) {
			rallyWaitingSince=DateTime.MinValue;
			rallyUniversalityWaitingSince=DateTime.MinValue;
			int value2=Math.Min(250,40+gauge.BeastHeartStacks*70);
			string text;
			if(UniversalityWindow(level)) {
				text=$"Rally: closing to {value2} TP for Universality ({gauge.BeastHeartStacks} stack(s))";
			}
			else {
				text=(flag8?$"Rally: {gauge.BeastHeartStacks} stack(s) -> {value2} TP":$"Rally at {gauge.BeastHeartStacks} stack(s) -> {value2} TP - it has been ready too long to keep holding");
			}
			LastRallyNote=$"{text} [Heart {HeldHeart}, half {((HeldInfinitiveHalf==0)?"none":HeldInfinitiveHalf.ToString())}, TP {gauge.Tp}, window {(UniversalityWindow(level)?"yes":"no")}{(flag11?", gave up waiting":string.Empty)}]";
			LastRallyAt=DateTime.UtcNow;
			return Decide(text,"Cooldown",44905u);
		}
		if(stacksLookSane&&BeastmasterActions.IsUnlocked(44905u,level)) {
			int beastHeartStacks=gauge.BeastHeartStacks;
			if(beastHeartStacks>0&&beastHeartStacks<num2) {
				LastReason=$"Banking Mastered Instinct for Rally ({beastHeartStacks}/{num2})";
				goto IL_0dc5;
			}
		}
		if(flag3&&!UniversalityWindow(level)&&gauge.BeastHeartStacks>=2&&BeastmasterActions.IsUnlocked(44905u,level)) {
			LastReason=$"Rally saved for the finisher: {gauge.BeastHeartStacks} Mastered Instinct, waiting for the combo that fills a half";
		}
		goto IL_0dc5;
	}
	private unsafe RotationDecision NextOpenerStep(int level,BeastmasterGaugeSnapshot gauge,ActionManager* actionManager,IBattleChara target) {
		switch(openerStep) {
		case 0:
		openerStep++;
		if(BeastmasterActions.IsUnlocked(44893u,level)&&RotationToggleOverrides.Get("Cooldowns",configuration.RotationUseCooldowns)&&RotationToggleOverrides.Get("GapClose",configuration.ShieldChargeGapClose)&&DistanceToTarget(target)>configuration.ShieldChargeMinDistance&&Ready(actionManager,44893u,target)) {
			if(ChargeIsSafe(target,out string why)) {
				GapCloseStatus="Charging - opener, the path is clear";
				return Decide("Opener: Shield Charge to close","Opener",44893u);
			}
			GapCloseStatus=why;
		}
		goto case 1;
		case 1:
		openerStep++;
		if(RotationToggleOverrides.Get("PowerActions",configuration.RotationUsePowerActions)&&RotationToggleOverrides.Get("Trick",baseline: true)&&BeastmasterActions.IsUnlocked(47093u,level)&&gauge.BeastPower>=100&&!waitingForTrick&&HeldHeart==InstinctAffinity.None&&!HasOwnStatus(4643u)&&(!RotationToggleOverrides.Get("TrickWaitsTp",configuration.RotationTrickWaitForTp)||!AnyAxeUnlocked(level)||gauge.Tp>=100)&&Ready(actionManager,47093u,target)) {
			return Decide("Opener: Trick for the first Heart","Opener",47093u);
		}
		goto case 2;
		case 2: {
			openerStep++;
			RotationDecision rotationDecision=NextComboStep(level,actionManager,target);
			if(!rotationDecision.HasAction) {
				return RotationDecision.None;
			}
			return rotationDecision with {
				Phase = "Opener",
				Reason = "Opener: build TP"
			};
		}
		default:
		return RotationDecision.None;
		}
	}
	private unsafe RotationDecision NextAxe(int level,BeastmasterGaugeSnapshot gauge,ActionManager* actionManager,IBattleChara target) {
		if(waitingForTrick||!RotationToggleOverrides.Get("PowerActions",configuration.RotationUsePowerActions)) {
			return RotationDecision.None;
		}
		if(HeldHeart==InstinctAffinity.None&&(DateTime.UtcNow-lastTrickDecidedAt).TotalSeconds<1.5) {
			LastReason="Waiting for the familiar's Trick to land before the axe";
			return RotationDecision.None;
		}
		int num=((Burning||WantedAffinity!=InstinctAffinity.None)?100:Math.Clamp(configuration.RotationAxeTpThreshold,100,250));
		if(gauge.Tp<num) {
			return RotationDecision.None;
		}
		if(UniversalityWindow(level)&&gauge.Tp>=250) {
			uint needHalf=BeastmasterActions.OppositeHalf(HeldInfinitiveHalf);
			uint num2=new uint[5] {
				BeastmasterActions.AxeFor(WantedAffinity),
				44884u,
				44888u,
				44887u,
				44889u
			}.Where((uint axe) => axe!=0&&BeastmasterActions.IntentionalHalf(BeastmasterActions.AffinityOf(axe))==needHalf&&BeastmasterActions.IsUnlocked(axe,level)&&Ready(actionManager,axe,target)).FirstOrDefault();
			if(num2!=0) {
				finisherPendingUntil=DateTime.UtcNow.AddSeconds(3.0);
				return Decide($"{BeastmasterActions.AffinityOf(num2)} axe at {gauge.Tp} TP: Universality finisher (opposite of the banked half)","Wheel",num2);
			}
		}
		if(WantedAffinity!=InstinctAffinity.None) {
			uint num3=BeastmasterActions.AxeFor(WantedAffinity);
			if(num3!=0&&BeastmasterActions.IsUnlocked(num3,level)&&Ready(actionManager,num3,target)) {
				return Decide($"{WantedAffinity} axe completes the combo from {HeldHeart} Heart (TP {gauge.Tp})","Wheel",num3);
			}
			if(num3!=0&&BeastmasterActions.IsUnlocked(num3,level)) {
				return RotationDecision.None;
			}
		}
		bool flag=configuration.RotationPoolForBurst&&!Burning&&HeldHeart==InstinctAffinity.None&&gauge.SummonDataId!=0&&gauge.BeastPower<100&&gauge.Tp<240&&BeastmasterActions.IsUnlocked(47093u,level)&&RotationToggleOverrides.Get("PowerActions",configuration.RotationUsePowerActions);
		if(flag&&!Stalled(ref axePoolWaitingSince,waiting: true,60f)) {
			LastReason=$"Pooling TP for the burst: Trick is at {gauge.BeastPower}/100, the axe waits to follow it (TP {gauge.Tp})";
			return RotationDecision.None;
		}
		if(!flag) {
			axePoolWaitingSince=DateTime.MinValue;
		}
		uint[] array=new uint[4] { 44889u, 44888u, 44887u, 44884u };
		foreach(uint num5 in array) {
			if(BeastmasterActions.IsUnlocked(num5,level)&&Ready(actionManager,num5,target)) {
				InstinctAffinity value=BeastmasterActions.AffinityOf(num5);
				return Decide((HeldHeart==InstinctAffinity.None)?$"{value} axe to open the wheel (TP {gauge.Tp})":$"{value} axe - wheel action unavailable (TP {gauge.Tp})","Wheel",num5);
			}
		}
		return RotationDecision.None;
	}
	private unsafe RotationDecision NextShieldCharge(int level,ActionManager* actionManager,IBattleChara target) {
		if(!RotationToggleOverrides.Get("Cooldowns",configuration.RotationUseCooldowns)||!BeastmasterActions.IsUnlocked(44893u,level)||!Ready(actionManager,44893u,target)) {
			return RotationDecision.None;
		}
		if(RotationToggleOverrides.Get("GapClose",configuration.ShieldChargeGapClose)) {
			float num=DistanceToTarget(target);
			int num2;
			if(BorrowedKinship==BeastKin.Wavekin) {
				num2=((num<=30f)?1:0);
				if(num2!=0) {
					GapCloseStatus=$"Holding - Wavekin lent and the target is {num:0.#}y away, within Quelling Wave's 30y";
				}
			}
			else {
				num2=0;
			}
			if(num2==0&&num>configuration.ShieldChargeMinDistance&&num<=20f) {
				if(ChargeIsSafe(target,out string why)) {
					GapCloseStatus="Charging - the path is clear";
					return Decide($"Shield Charge to close {num:0.#}y","Gap close",44893u);
				}
				GapCloseStatus=why;
			}
		}
		if(RotationToggleOverrides.Get("ChargeAoe",configuration.ShieldChargeAoe)) {
			float num3=Math.Max(configuration.ShieldChargeAoeRadius,6f);
			int num4=EnemiesNearPosition(target.Position,num3);
			if(num4>=configuration.ShieldChargeAoeCount) {
				if(!ChargeIsSafe(target,out string why2,gapClose: false)) {
					GapCloseStatus=why2;
					return RotationDecision.None;
				}
				GapCloseStatus=$"Charging into the pack - {num4} enemies around the target";
				return Decide($"Shield Charge: {num4} enemies within {num3:0}y of {target.Name.TextValue}","AoE",44893u);
			}
			GapCloseStatus=$"Waiting for a pack: {num4} of {configuration.ShieldChargeAoeCount} enemies within {num3:0}y of the target";
		}
		return RotationDecision.None;
	}
	private bool IsBlockedEncounter(string encounter) {
		foreach(string shieldChargeBlockedEncounter in configuration.ShieldChargeBlockedEncounters) {
			if(!string.IsNullOrWhiteSpace(shieldChargeBlockedEncounter)) {
				string text=shieldChargeBlockedEncounter.Trim();
				if(encounter.Contains(text,StringComparison.OrdinalIgnoreCase)||text.Contains(encounter,StringComparison.OrdinalIgnoreCase)) {
					return true;
				}
			}
		}
		return false;
	}
	private bool ChargeIsSafe(IBattleChara target,out string why,bool gapClose=true) {
		if(!bossMod.IsInstalled) {
			why=string.Empty;
			return true;
		}
		string text=(bossMod.InEncounter?bossMod.EncounterName:string.Empty);
		if(gapClose&&text.Length>0&&IsBlockedEncounter(text)) {
			why="Gap close is switched off for "+text;
			return false;
		}
		if(!DashIsSafe(target)) {
			why=((bossMod.DangerIn<30f)?$"Holding the charge - the target's ground is unsafe for {bossMod.DangerIn:0.#}s":"Holding the charge - the target's ground is unsafe");
			return false;
		}
		why=string.Empty;
		return true;
	}
	private bool DashIsSafe(IBattleChara target) {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null||!bossMod.IsInstalled) {
			return true;
		}
		return bossMod.IsDashSafe(localPlayer.Position,target.Position);
	}
	private static float DistanceToTarget(IBattleChara target) {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return 0f;
		}
		float num=Vector3.Distance(localPlayer.Position,target.Position);
		return Math.Max(0f,num-target.HitboxRadius-localPlayer.HitboxRadius);
	}
	private static int EnemiesNearPosition(Vector3 centre,float radius) {
		int num=0;
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(item is IBattleNpc { ObjectKind: ObjectKind.BattleNpc,IsDead: false,CurrentHp: not 0u,IsTargetable: not false } battleNpc&&Vector3.Distance(battleNpc.Position,centre)<=radius) {
				num++;
			}
		}
		return num;
	}
	private void NoteCaptureSkipped(IBattleChara target,string why) {
		(uint,string) tuple=captureSkipLogged;
		uint entityId=target.EntityId;
		if(tuple.Item1!=entityId||!(tuple.Item2==why)) {
			captureSkipLogged=(target.EntityId,why);
			DalamudApi.Log.Information("Capture not used on {Name} (level {Level}, territory {Territory}): {Why}.",target.Name.TextValue,target.Level,DalamudApi.ClientState.TerritoryType,why);
		}
	}
	private unsafe RotationDecision NextCapture(int level,ActionManager* actionManager,IBattleChara target) {
		if(!RotationToggleOverrides.Get("Capture",configuration.AutoCaptureTryCapture)||!BeastmasterActions.IsUnlocked(44880u,level)) {
			return RotationDecision.None;
		}
		if(BeastmasterCaptureAttempts.AlreadyTried(target.GameObjectId)) {
			return RotationDecision.None;
		}
		float num=((target.MaxHp==0)?100f:((float)target.CurrentHp*100f/(float)target.MaxHp));
		if(num>configuration.CaptureHpThreshold) {
			return RotationDecision.None;
		}
		CaptureVerdict captureVerdict=BeastmasterCaptureTargeting.Evaluate(target,level,(ushort)DalamudApi.ClientState.TerritoryType,progressService);
		if(!captureVerdict.ShouldCapture) {
			NoteCaptureSkipped(target,"verdict: "+captureVerdict.Reason);
			return RotationDecision.None;
		}
		if(!Ready(actionManager,44880u,target)) {
			NoteCaptureSkipped(target,$"Capture is not usable on it right now (range {DistanceToTarget(target):0.#}y)");
			return RotationDecision.None;
		}
		return Decide($"Capture: {target.Name.TextValue} at {num:0}% HP","Capture",44880u);
	}
	private static bool TargetHasOwnStatus(IBattleChara target,uint statusId) {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return false;
		}
		foreach(IStatus status in target.StatusList) {
			if(status.StatusId==statusId&&(status.SourceId==localPlayer.EntityId||status.SourceId==localPlayer.GameObjectId)) {
				return true;
			}
		}
		return false;
	}
	public void NotifyActionUsed(uint actionId,IBattleChara target) {
		BeastmasterActionRequest.NoteUsed(actionId);
		if((actionId==44881||actionId==44892||actionId==44894)?true:false) {
			summonGuardUntil=DateTime.UtcNow.AddSeconds(5.0);
		}
		if(actionId==47093) {
			trickWindow.Begin(DateTime.UtcNow);
		}
		if(actionId==44880) {
			BeastmasterCaptureAttempts.Record(target.GameObjectId);
		}
	}
	public void NoteCycleIdle(string status,string detail) {
		if(!RotationToggleOverrides.Get("Cycle",configuration.RotationCycleFamiliars)) {
			SetCycle("Cycling off","Switch Cycle on to have Parting Blow dismiss each familiar so the next can come out.");
		}
		else if(configuration.RotationBattlehornSlot!=0) {
			SetCycle($"Battlehorn {configuration.RotationBattlehornSlot} pinned","Cycling needs the Battlehorn selection on Auto - a pinned slot means you have asked for one specific familiar, so there is nothing to cycle to.");
		}
		else {
			SetCycle(status,detail);
		}
	}
	private void SetCycle(string status,string detail) {
		CycleStatus=status;
		CycleDetail=detail;
	}
	private unsafe RotationDecision NextBeastMode(int level,ActionManager* actionManager,IBattleChara target,bool outOfRange) {
		BeastKin borrowedKinship=BorrowedKinship;
		if(!RotationToggleOverrides.Get("BeastMode",configuration.RotationUseBeastMode)||borrowedKinship==BeastKin.Unknown||!BeastmasterActions.IsUnlocked(44886u,level)) {
			return RotationDecision.None;
		}
		if((borrowedKinship!=BeastKin.Cloudkin&&borrowedKinship!=BeastKin.Wavekin)||1==0) {
			return RotationDecision.None;
		}
		var (flag,reason)=ShouldUseBeastMode(borrowedKinship,target,outOfRange);
		if(!flag) {
			return RotationDecision.None;
		}
		if(!BeastModeReady(actionManager,target,reason)) {
			return RotationDecision.None;
		}
		CrucibleFightPlan.NoteBeastMode(borrowedKinship);
		return Decide(reason,"Kinship",44886u);
	}
	public void NoteDutyActionAttempt() {
		dutyActionSettlesAt=DateTime.UtcNow.AddMilliseconds(1500.0);
	}
	private unsafe bool BeastModeReady(ActionManager* actionManager,IBattleChara target,string reason) {
		BeastKin borrowedKinship=BorrowedKinship;
		bool flag=((borrowedKinship==BeastKin.Wavekin||borrowedKinship==BeastKin.Soulkin)?true:false);
		ulong targetId=(flag?target.GameObjectId:SelfOr(target.GameObjectId,44886u));
		uint adjustedActionId=actionManager->GetAdjustedActionId(44886u);
		uint actionStatus=actionManager->GetActionStatus(ActionType.Action,44886u,targetId,checkRecastActive: true,checkCastingActive: true,null);
		uint num=((adjustedActionId!=44886)?actionManager->GetActionStatus(ActionType.Action,adjustedActionId,targetId,checkRecastActive: true,checkCastingActive: true,null):actionStatus);
		if(actionStatus==0||num==0) {
			return true;
		}
		if((DateTime.UtcNow-beastModeNotReadyLoggedAt).TotalSeconds>=3.0) {
			beastModeNotReadyLoggedAt=DateTime.UtcNow;
			DalamudApi.Log.Information("Beast Mode wanted ({Reason}) but not ready: Beast Mode status {Base}; swapped action {Adjusted} status {Adj}.",reason,actionStatus,adjustedActionId,num);
		}
		return false;
	}
	private static bool IsMorpho(IBattleChara? target) {
		if(target!=null) {
			CrucibleGuideFight? active=CrucibleFightPlan.Active;
			if((object)active!=null&&active.Name.Contains("Drake",StringComparison.OrdinalIgnoreCase)) {
				return target.Name.TextValue.Contains("Morpho",StringComparison.OrdinalIgnoreCase);
			}
		}
		return false;
	}
	public void UpdatePetStance(IBattleChara? target) {
		if(CrucibleFightPlan.Applies(configuration)) {
			CrucibleGuideFight? active=CrucibleFightPlan.Active;
			if((object)active!=null&&active.Name.Contains("Drake",StringComparison.OrdinalIgnoreCase)) {
				if(MorphoPhase()) {
					morphoEpisode=true;
					morphosSeen|=AnyMorphoAlive();
					IBattleNpc battleNpc=FamiliarOut();
					uint num=BeastmasterGaugeSnapshot.Read().SummonDataId;
					if(num==0&&battleNpc!=null) {
						num=battleNpc.EntityId;
					}
					if((DateTime.UtcNow-phaseLoggedAt).TotalSeconds>=3.0) {
						phaseLoggedAt=DateTime.UtcNow;
						DalamudApi.Log.Information("Morpho phase: active, familiar out {Familiar}, steadied {Steadied}, morphos seen {Seen}.",battleNpc?.Name.TextValue ?? "none",petSteadyed,morphosSeen);
					}
					if(num==0) {
						petSteadyed=false;
					}
					else if((!petSteadyed||num!=steadiedSummon)&&(DateTime.UtcNow-petSteadyTriedAt).TotalSeconds>=0.3) {
						petSteadyTriedAt=DateTime.UtcNow;
						petSteadyed=BeastmasterPetCommands.Send("Steady");
						steadiedSummon=(petSteadyed?num:0u);
						DalamudApi.Log.Information("Morpho phase: familiar ordered to Steady -> {Result}.",petSteadyed?"sent":"not sent");
					}
				}
				else if(morphoEpisode&&morphosSeen&&(DateTime.UtcNow-petSteadyTriedAt).TotalSeconds>=2.0&&!AnyMorphoAlive()) {
					petSteadyTriedAt=DateTime.UtcNow;
					if(BeastmasterPetCommands.Send("Guard")) {
						morphoEpisode=false;
						morphosSeen=false;
						petSteadyed=false;
						DalamudApi.Log.Information("All Morphos fed: familiar ordered back to Guard.");
					}
				}
				return;
			}
		}
		petSteadyed=false;
		morphoEpisode=false;
		morphosSeen=false;
	}
	private bool MorphoPhase() {
		if(CrucibleFightPlan.Applies(configuration)) {
			CrucibleGuideFight? active=CrucibleFightPlan.Active;
			if((object)active!=null&&active.Name.Contains("Drake",StringComparison.OrdinalIgnoreCase)) {
				DateTime? dateTime=morphoRestrictSince;
				if(dateTime.HasValue) {
					DateTime valueOrDefault=dateTime.GetValueOrDefault();
					bool flag=AnyAliveNamed("Morpho");
					morphosSeen|=flag;
					if((!morphosSeen||flag)&&(morphosSeen||!((DateTime.UtcNow-valueOrDefault).TotalSeconds>90.0))) {
						return true;
					}
					morphoRestrictSince=null;
				}
				if(AnyAliveNamed("Drake")||AnyAliveNamed("Barbmole")) {
					drakeSeenAlive=true;
					phaseGapSince=null;
					return false;
				}
				DateTime valueOrDefault2=phaseGapSince.GetValueOrDefault();
				if(!phaseGapSince.HasValue) {
					valueOrDefault2=DateTime.UtcNow;
					phaseGapSince=valueOrDefault2;
				}
				if(!morphosSeen&&!AnyAliveNamed("Morpho")&&(DateTime.UtcNow-phaseGapSince.Value).TotalSeconds>20.0) {
					return false;
				}
				if(!AnyAliveNamed("Morpho")) {
					if(drakeSeenAlive) {
						return !morphosSeen;
					}
					return false;
				}
				return true;
			}
		}
		drakeSeenAlive=false;
		morphoRestrictSince=null;
		return false;
	}
	private bool MorphoRefreshSummonAllowed() {
		if(MorphoPhase()) {
			CrucibleGuideFight active=CrucibleFightPlan.Active;
			if((object)active!=null&&active.BorrowKins.Length!=0) {
				BeastKin value=CrucibleFightPlan.PlanLent(BorrowedKinship);
				if(active.BorrowKins.Contains(value,null)) {
					return !(BeastmasterKinship.BorrowedRemaining()>32f);
				}
				return true;
			}
		}
		return false;
	}
	private static bool AnyAliveNamed(string name) {
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(item is IBattleNpc battleNpc&&item.ObjectKind==ObjectKind.BattleNpc&&!battleNpc.IsDead&&battleNpc.CurrentHp!=0&&battleNpc.Name.TextValue.Contains(name,StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
		}
		return false;
	}
	private static bool AnyMorphoAlive() {
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(item is IBattleNpc battleNpc&&item.ObjectKind==ObjectKind.BattleNpc&&!battleNpc.IsDead&&battleNpc.CurrentHp!=0&&battleNpc.Name.TextValue.Contains("Morpho",StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
		}
		return false;
	}
	private (bool Use,string Reason) ShouldUseBeastMode(BeastKin kin,IBattleChara target,bool outOfRange) {
		switch(kin) {
		case BeastKin.Wavekin:
		if(CrucibleFightPlan.Applies(configuration)&&IsMorpho(target)) {
			if(target.MaxHp!=0&&(double)target.CurrentHp*100.0/(double)target.MaxHp<50.0) {
				return (Use: false,Reason: string.Empty);
			}
			return (Use: true,Reason: "Quelling Wave on the Morpho - from range, so it is weakened without the familiar or a melee combo finishing it");
		}
		if(CrucibleFightPlan.Applies(configuration)&&CrucibleFightPlan.DispelPending(configuration)) {
			return (Use: true,Reason: "Quelling Wave to dispel the buff the strategy wants gone");
		}
		if(CrucibleFightPlan.Applies(configuration)) {
			CrucibleGuideFight active=CrucibleFightPlan.Active;
			if((object)active!=null) {
				string[] dispels=active.Dispels;
				if(dispels!=null&&dispels.Length>0) {
					return (Use: false,Reason: string.Empty);
				}
			}
		}
		if(outOfRange) {
			return (Use: true,Reason: $"Quelling Wave at {DistanceToTarget(target):0.#}y - the one attack you have at range");
		}
		if(!RotationToggleOverrides.Get("WaveInMelee",configuration.BeastModeQuellingWaveInMelee)) {
			return (Use: false,Reason: string.Empty);
		}
		return (Use: true,Reason: "Quelling Wave: 350p and TP, set to fire in melee too");
		case BeastKin.Soulkin:
		if(CrucibleFightPlan.Applies(configuration)) {
			if(!CrucibleFightPlan.InterruptWorthIt(configuration,target,out string name)) {
				return (Use: false,Reason: string.Empty);
			}
			return (Use: true,Reason: "Soul Crush to interrupt "+name+" - the strategy's interrupt");
		}
		if(target==null||!target.IsCasting||!target.IsCastInterruptible) {
			return (Use: false,Reason: string.Empty);
		}
		return (Use: true,Reason: "Soul Crush to interrupt "+BeastmasterGameData.ActionName(target.CastActionId,"a cast")+" - 1000p and +100 TP if it lands");
		case BeastKin.Beastkin:
		case BeastKin.Vilekin:
		case BeastKin.Scalekin: {
			if(CrucibleFightPlan.Applies(configuration)) {
				EnemyCast enemyCast=CrucibleFightPlan.Observe(configuration);
				if(enemyCast!=null&&(enemyCast.IsBuster||(enemyCast.IsRaidwide&&kin==BeastKin.Scalekin)||(!enemyCast.IsRaidwide&&kin!=BeastKin.Vilekin&&(enemyCast.Kind==DamageKind.Unknown||enemyCast.Kind==CrucibleFightPlan.KindOfKin(kin))))) {
					return (Use: true,Reason: BeastmasterKinship.Effect(kin)+" - "+enemyCast.Name+" is being cast"+((enemyCast.Kind==DamageKind.Unknown)?string.Empty:(" ("+enemyCast.Kind.ToString().ToLowerInvariant()+")")));
				}
				if(!(PlayerHpPercent()<configuration.KinshipMitigationHpThreshold)) {
					return (Use: false,Reason: string.Empty);
				}
				return (Use: true,Reason: $"{BeastmasterKinship.Effect(kin)} - HP is {PlayerHpPercent():0}%");
			}
			float num=PlayerHpPercent();
			if(num<configuration.KinshipMitigationHpThreshold) {
				return (Use: true,Reason: $"{BeastmasterKinship.Effect(kin)} - HP is {num:0}%");
			}
			if(!configuration.KinshipMitigationOnCooldown||!DalamudApi.Condition[ConditionFlag.InCombat]) {
				return (Use: false,Reason: string.Empty);
			}
			return (Use: true,Reason: BeastmasterKinship.Effect(kin)+" - kept up on cooldown");
		}
		case BeastKin.Seedkin:
		if(!RotationToggleOverrides.Get("Aoe",configuration.RotationUseAoe)||EnemiesNearTarget<configuration.RotationAoeThreshold) {
			return (Use: false,Reason: string.Empty);
		}
		return (Use: true,Reason: $"Seedsower on {EnemiesNearTarget} enemies: -10% damage dealt and a DoT");
		case BeastKin.Ashkin:
		if(!HasRemovableDebuff()) {
			return (Use: false,Reason: string.Empty);
		}
		return (Use: true,Reason: "Scouring Ash: cleanse, +10% HP and +100 TP on the removal");
		case BeastKin.Cloudkin:
		if(!(RotationToggleOverrides.Get("CloudSkim",configuration.BeastModeAllowCloudSkim)&outOfRange)) {
			return (Use: false,Reason: string.Empty);
		}
		return (Use: true,Reason: "Cloud Skim to close the distance");
		default:
		return (Use: false,Reason: string.Empty);
		}
	}
	internal static bool HasRemovableDebuff() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return false;
		}
		foreach(IStatus status in localPlayer.StatusList) {
			if(status.StatusId==0) {
				continue;
			}
			try {
				if(DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>().TryGetRow(status.StatusId,out var row)&&row.StatusCategory==2&&!row.IsPermanent) {
					return true;
				}
			}
			catch(Exception exception) {
				DalamudApi.Log.Warning(exception,"Could not classify status {Id}.",status.StatusId);
			}
		}
		return false;
	}
	private bool Stalled(ref DateTime since,bool waiting) {
		return Stalled(ref since,waiting,configuration.RotationRallyStallSeconds);
	}
	private bool Stalled(ref DateTime since,bool waiting,float limit) {
		if(!waiting||limit<=0f) {
			since=DateTime.MinValue;
			return false;
		}
		if(since==DateTime.MinValue) {
			since=DateTime.UtcNow;
			return false;
		}
		return (DateTime.UtcNow-since).TotalSeconds>=(double)limit;
	}
	private static bool AnyAxeUnlocked(int level) {
		return BeastmasterActions.IsUnlocked(44884u,level);
	}
	private void HandlePotionRequest() {
		if(BeastmasterActionRequest.IsPending(900001u)&&!((DateTime.UtcNow-potionTriedAt).TotalSeconds<1.5)) {
			potionTriedAt=DateTime.UtcNow;
			if(BeastmasterPotion.TryUse()) {
				BeastmasterActionRequest.NoteUsed(900001u);
			}
		}
	}
	private void StopAutoAttackIfOff() {
		if(!RotationToggleOverrides.Get("AutoAttack",configuration.RotationAutoAttack)) {
			BeastmasterAutoAttack.Stop();
		}
	}
	private bool OtherEnemyStillHealthy(IBattleChara target) {
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(item is IBattleNpc battleNpc&&item.ObjectKind==ObjectKind.BattleNpc&&battleNpc.GameObjectId!=target.GameObjectId&&battleNpc.BattleNpcKind==BattleNpcSubKind.Combatant&&!battleNpc.IsDead&&battleNpc.CurrentHp!=0&&battleNpc.MaxHp!=0&&battleNpc.IsTargetable&&(battleNpc.StatusFlags&StatusFlags.InCombat)!=0&&(float)battleNpc.CurrentHp*100f/(float)battleNpc.MaxHp>=configuration.RotationBurnBelowEnemyHpPercent) {
				return true;
			}
		}
		return false;
	}
	private unsafe RotationDecision NextOneWithNature(int level,BeastmasterGaugeSnapshot gauge,ActionManager* actionManager,IBattleChara target) {
		RotationToggleOverrides.Familiar=gauge.SummonEntry?.Number ?? 0;
		OneWithNatureChoice? oneWithNatureChoice=RotationToggleOverrides.NatureChoice(configuration,gauge.SummonEntry?.Number ?? 0);
		if(!oneWithNatureChoice.HasValue) {
			return RotationDecision.None;
		}
		if(!HasOwnStatus(4601u)) {
			if((DateTime.UtcNow-natureNotReadyLoggedAt).TotalSeconds>=3.0) {
				natureNotReadyLoggedAt=DateTime.UtcNow;
				DalamudApi.Log.Information("One with Nature wanted ({Selected}, familiar {Familiar}) but the buff itself is not up.",oneWithNatureChoice,gauge.SummonEntry?.Name ?? "none");
			}
			return RotationDecision.None;
		}
		uint num=((oneWithNatureChoice==OneWithNatureChoice.Borrow)?44895u:44890u);
		if(!BeastmasterActions.IsUnlocked(num,level)) {
			return RotationDecision.None;
		}
		if(!Ready(actionManager,num,target)) {
			if((DateTime.UtcNow-natureNotReadyLoggedAt).TotalSeconds>=3.0) {
				natureNotReadyLoggedAt=DateTime.UtcNow;
				ulong targetId=SelfOr(target.GameObjectId,num);
				uint adjustedActionId=actionManager->GetAdjustedActionId(num);
				uint actionStatus=actionManager->GetActionStatus(ActionType.Action,num,targetId,checkRecastActive: true,checkCastingActive: true,null);
				uint num2=((adjustedActionId!=num)?actionManager->GetActionStatus(ActionType.Action,adjustedActionId,targetId,checkRecastActive: true,checkCastingActive: true,null):actionStatus);
				DalamudApi.Log.Information("One with Nature wanted ({Selected}, familiar {Familiar}) but not ready: base action {Base} status {BaseStatus}; swapped action {Adjusted} status {AdjStatus}.",oneWithNatureChoice,gauge.SummonEntry?.Name ?? "none",num,actionStatus,adjustedActionId,num2);
			}
			return RotationDecision.None;
		}
		string text=BeastmasterGameData.ActionName(num,"One with Nature");
		if(num==44890) {
			BeastmasterCatalogEntry summonEntry=gauge.SummonEntry;
			if((object)summonEntry!=null&&summonEntry.Number==50&&CrucibleFightPlan.Applies(configuration)&&temperedForAddsAt==DateTime.MinValue) {
				temperedForAddsAt=DateTime.UtcNow;
				DateTime valueOrDefault=morphoRestrictSince.GetValueOrDefault();
				if(!morphoRestrictSince.HasValue) {
					valueOrDefault=DateTime.UtcNow;
					morphoRestrictSince=valueOrDefault;
				}
			}
		}
		return Decide(text+": spending One with Nature before the familiar leaves","Cooldown",num);
	}
	private unsafe RotationDecision NextAoeAction(int level,BeastmasterGaugeSnapshot gauge,ActionManager* actionManager,IBattleChara target) {
		if(RotationToggleOverrides.Get("PartingBlow",configuration.RotationAllowPartingBlow)&&!MorphoPhase()&&BeastmasterActions.IsUnlocked(44891u,level)&&Ready(actionManager,44891u,target)) {
			if(CanSpendFamiliar(gauge,out string _,out string detail)) {
				return Decide($"Parting Blow: {EnemiesNearTarget} enemies (familiar retreats)","AoE",44891u);
			}
			LastReason="Holding Parting Blow - "+detail;
		}
		return RotationDecision.None;
	}
	private unsafe RotationDecision NextComboStep(int level,ActionManager* actionManager,IBattleChara target,bool outOfRange=false) {
		uint action=actionManager->Combo.Action;
		bool flag=actionManager->Combo.Timer>0f;
		if(flag&&action==44879&&BeastmasterActions.IsUnlocked(44883u,level)&&Ready(actionManager,44883u,target)) {
			return Decide("Combo 2: Axeblade Bite","Filler",44883u);
		}
		if(flag&&action==44883&&BeastmasterActions.IsUnlocked(44885u,level)&&Ready(actionManager,44885u,target)) {
			return Decide("Combo 3: Shieldsplitter (+15 TP)","Filler",44885u);
		}
		if(Ready(actionManager,44879u,target)) {
			return Decide("Combo 1: Smash Axe","Filler",44879u);
		}
		if(!outOfRange) {
			return Decide("Waiting for the global cooldown","Filler",0u);
		}
		return Decide(PlayerMovement.IsMoving?"Walking into range":"Target is out of range","Approach",0u);
	}
	private bool UtilityHandoffDue(BeastmasterGaugeSnapshot gauge) {
		if(PostMeteorDue()) {
			return true;
		}
		if(MorphoPhase()) {
			return false;
		}
		if(CrucibleFightPlan.Applies(configuration)) {
			BeastmasterCatalogEntry summonEntry=gauge.SummonEntry;
			if((object)summonEntry!=null&&!HasOwnStatus(4601u)&&!CrucibleFightPlan.WantsOpener(out var _)) {
				BeastKin beastKin=BeastmasterKinship.KinOf(summonEntry.Number);
				CrucibleGuideFight active=CrucibleFightPlan.Active;
				if(beastKin!=BeastKin.Unknown&&active.BorrowKins.Contains(beastKin,null)&&BorrowedKinship==beastKin) {
					return CrucibleFightPlan.UseFor(configuration,CrucibleFightPlan.ActiveBoard,active,summonEntry.Number).Use==PlannedUse.Borrow;
				}
				return false;
			}
		}
		return false;
	}
	private static double AverageHpPercent(string[] names) {
		double num=0.0;
		int num2=0;
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			IBattleNpc npc=item as IBattleNpc;
			if(npc!=null&&item.ObjectKind==ObjectKind.BattleNpc&&!npc.IsDead&&npc.CurrentHp!=0&&npc.MaxHp!=0&&npc.IsTargetable&&names.Any((string n) => npc.Name.TextValue.Contains(n,StringComparison.OrdinalIgnoreCase))) {
				num+=(double)npc.CurrentHp*100.0/(double)npc.MaxHp;
				num2++;
			}
		}
		if(num2!=0) {
			return num/(double)num2;
		}
		return -1.0;
	}
	private static int CountNamedNear(string[] names,Vector3 at,float radius,bool anyTargetable=true) {
		int num=0;
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			IBattleNpc npc=item as IBattleNpc;
			if(npc!=null&&item.ObjectKind==ObjectKind.BattleNpc&&!npc.IsDead&&npc.CurrentHp!=0&&(anyTargetable||npc.IsTargetable)&&Vector3.Distance(npc.Position,at)<=radius&&names.Any((string n) => npc.Name.TextValue.Contains(n,StringComparison.OrdinalIgnoreCase))) {
				num++;
			}
		}
		return num;
	}
	private static bool AnyLiveEnemyNamed(string[] names) {
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			IBattleNpc npc=item as IBattleNpc;
			if(npc!=null&&item.ObjectKind==ObjectKind.BattleNpc&&!npc.IsDead&&npc.CurrentHp!=0&&names.Any((string n) => npc.Name.TextValue.Contains(n,StringComparison.OrdinalIgnoreCase))) {
				return true;
			}
		}
		return false;
	}
	private bool PostMeteorDue() {
		if(temperedForAddsAt==DateTime.MinValue||!CrucibleFightPlan.Applies(configuration)||HasOwnStatus(4601u)) {
			return false;
		}
		double totalSeconds=(DateTime.UtcNow-temperedForAddsAt).TotalSeconds;
		string[] temperedFor=CrucibleFightPlan.Active.TemperedFor;
		if(totalSeconds<3.5||totalSeconds>30.0||temperedFor.Length==0||!AnyLiveEnemyNamed(temperedFor)) {
			return false;
		}
		double num=AverageHpPercent(temperedFor.Append(CrucibleFightPlan.Active.Name.Replace(" Piece",string.Empty)).ToArray());
		if(!(totalSeconds>=12.0)) {
			if(num>=0.0) {
				return num<50.0;
			}
			return false;
		}
		return true;
	}
	private bool CanSpendFamiliar(BeastmasterGaugeSnapshot gauge,out string reason,out string detail) {
		if(gauge.SummonDataId==0) {
			reason="No familiar out";
			detail="Parting Blow orders your familiar to do something, so it needs one out.";
			return false;
		}
		if(!RotationToggleOverrides.Get("PartingBlow",baseline: true)) {
			reason="Reaction hold";
			detail="Parting Blow and familiar dismissal are temporarily held by an external profile.";
			return false;
		}
		if(AddBurstTiming.Holding) {
			reason="Saving for add";
			detail="Parting Blow is held for "+AddBurstTiming.HoldReason+", which has to be burst the moment it spawns (see AddBurstTiming).";
			return false;
		}
		if(trickWindow.Waiting(DateTime.UtcNow,BeastmasterStatusReader.HeldHeart()!=InstinctAffinity.None,gauge.SummonDataId!=0)) {
			reason="Trick resolving";
			detail="Let the familiar deliver its commanded Trick before Parting Blow dismisses it.";
			return false;
		}
		if(UtilityHandoffDue(gauge)) {
			reason=string.Empty;
			detail=string.Empty;
			return true;
		}
		if(familiarSummonedAt==DateTime.MinValue) {
			reason="Timing familiar";
			detail="Waiting to see how long the current familiar has been out before spending it.";
			return false;
		}
		double totalSeconds=(DateTime.UtcNow-familiarSummonedAt).TotalSeconds;
		int rotationCycleDwellSeconds=configuration.RotationCycleDwellSeconds;
		if(totalSeconds<(double)rotationCycleDwellSeconds) {
			reason=$"Out {totalSeconds:0}s / {rotationCycleDwellSeconds}s";
			detail=$"The familiar has to be out for {rotationCycleDwellSeconds}s before Parting Blow may spend it, so it "+"earns its keep rather than being blown up a moment after arriving.";
			return false;
		}
		bool flag=totalSeconds>=(double)(rotationCycleDwellSeconds*2);
		if(RotationToggleOverrides.Get("SpendPower",configuration.RotationCycleWaitForSpentPower)&&gauge.BeastPower>=100&&!flag) {
			reason=$"Trick left ({gauge.BeastPower} BP)";
			detail=$"Beast Power leaves with the familiar and is what Trick spends, so dismissing a full one throws a Trick away. Going anyway in {(double)(rotationCycleDwellSeconds*2)-totalSeconds:0}s.";
			return false;
		}
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		bool flag2=localPlayer!=null&&RotationToggleOverrides.NatureChoice(configuration).HasValue&&RotationToggleOverrides.Get("Cooldowns",configuration.RotationUseCooldowns)&&BeastmasterActions.IsUnlocked(44890u,localPlayer.Level);
		if((RotationToggleOverrides.Get("NeedVantage",configuration.RotationPartingBlowNeedsVantage)&flag2)&&!flag&&!HasOwnStatus(4614u)) {
			reason="No Vantage";
			detail="Lingering Vantage takes Parting Blow from 1,000 to 1,500 potency, and One with Nature "+$"grants it. Going anyway in {(double)(rotationCycleDwellSeconds*2)-totalSeconds:0}s.";
			return false;
		}
		reason=string.Empty;
		detail=string.Empty;
		return true;
	}
	private unsafe RotationDecision NextDutyEnmity(int level,BeastmasterGaugeSnapshot gauge,ActionManager* actionManager,IBattleChara target) {
		if(DutyActionSettling) {
			CoveredStatus="Duty action settling - continuing attacks";
			return RotationDecision.None;
		}
		if(bossMod.PreferChallenge||MorphoPhase()) {
			return NextDrakeMorphoChallenge(gauge);
		}
		string encounter=(bossMod.InEncounter?bossMod.EncounterName:string.Empty);
		bool flag=CrucibleFightPlan.HandedToReactionHelper(configuration,encounter);
		if(CrucibleEnmityPlan.Applies(configuration)&&!flag&&!RotationToggleOverrides.IsHeld("KeepCovered")) {
			return NextPlannedEnmity(level,gauge,actionManager,target);
		}
		if(flag&&!RotationToggleOverrides.IsHeld("KeepCovered")) {
			CoveredStatus="Handed to Reaction Helper - BMH is not managing Snarl/Challenge";
			return RotationDecision.None;
		}
		if(!RotationToggleOverrides.Get("KeepCovered",configuration.RotationKeepCovered)) {
			if(gauge.SummonDataId==0) {
				CoveredStatus="Challenge: no familiar out";
				return RotationDecision.None;
			}
			if(!BeastmasterDutyActions.IsAvailable(46750u)) {
				CoveredStatus="Challenge not offered here";
				return RotationDecision.None;
			}
			if(!BeastmasterDutyActions.CanUse(46750u)) {
				CoveredStatus="Challenge on cooldown";
				return RotationDecision.None;
			}
			CoveredStatus="Challenging";
			return Decide("Challenge: keeping enemy enmity on you","Challenge",46750u);
		}
		if(gauge.SummonDataId==0) {
			CoveredStatus="No familiar out";
			return RotationDecision.None;
		}
		IBattleNpc battleNpc=FamiliarOut();
		if(battleNpc!=null&&battleNpc.MaxHp!=0&&(double)battleNpc.CurrentHp*100.0/(double)battleNpc.MaxHp<=12.0&&RotationToggleOverrides.Get("PartingBlow",baseline: true)&&Ready(actionManager,44891u,target)) {
			return Decide("Parting Blow: the familiar is nearly dead - send it away before it dies","Cycle",44891u);
		}
		if(!BeastmasterDutyActions.IsAvailable(46751u)) {
			CoveredStatus="Snarl not offered here";
			return RotationDecision.None;
		}
		if(BeastmasterDutyActions.IsCovered()) {
			IBattleNpc battleNpc2=FamiliarOut();
			double num=((battleNpc2!=null&&battleNpc2.MaxHp!=0)?((double)battleNpc2.CurrentHp*100.0/(double)battleNpc2.MaxHp):100.0);
			IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
			double num2=((localPlayer!=null&&localPlayer.MaxHp!=0)?((double)localPlayer.CurrentHp*100.0/(double)localPlayer.MaxHp):100.0);
			bool flag2=ReplacementReady(actionManager,gauge);
			if(configuration.RotationCoveredReleaseHp>0&&num<=(double)configuration.RotationCoveredReleaseHp&&!flag2) {
				CoveredStatus=$"Covered kept - familiar {num:0}% but no other familiar is ready to swap to";
			}
			else if(configuration.RotationCoveredReleaseHp>0&&num<=(double)configuration.RotationCoveredReleaseHp) {
				if(num2<50.0) {
					CoveredStatus=$"Covered kept - familiar {num:0}% but you are at {num2:0}%";
					return RotationDecision.None;
				}
				if(BeastmasterDutyActions.IsAvailable(46750u)&&BeastmasterDutyActions.CanUse(46750u)) {
					CoveredStatus=$"Releasing - familiar at {num:0}%";
					DalamudApi.Log.Information("Challenge: releasing Covered, familiar at {Hp:0}% (release at {Release}%).",num,configuration.RotationCoveredReleaseHp);
					return Decide($"Challenge: familiar at {num:0}% - taking the enemy back so it survives","Challenge",46750u);
				}
				CoveredStatus=$"Familiar {num:0}% - Challenge not ready";
				return RotationDecision.None;
			}
			CoveredStatus="Covered is up";
			return RotationDecision.None;
		}
		IBattleNpc battleNpc3=FamiliarOut();
		double num3=((battleNpc3!=null&&battleNpc3.MaxHp!=0)?((double)battleNpc3.CurrentHp*100.0/(double)battleNpc3.MaxHp):100.0);
		int num4=Math.Max(configuration.RotationCoveredFamiliarHpFloor,configuration.RotationCoveredReleaseHp);
		if(!ReplacementReady(actionManager,gauge)) {
			num4=Math.Min(num4,12);
		}
		if(num3<=(double)num4) {
			CoveredStatus=$"Holding - familiar is at {num3:0}%";
			DalamudApi.Log.Information("Snarl held: familiar HP {Hp:0}% is at or below the floor ({Floor}%).",num3,num4);
			return RotationDecision.None;
		}
		double num5=SecondsUntilSwap(gauge);
		if(num5>=0.0&&num5<=(double)configuration.RotationCoveredHoldSeconds) {
			CoveredStatus=$"Holding - swapping in {num5:0}s";
			DalamudApi.Log.Information("Snarl held: swap predicted in {Until:0.0}s (hold window {Hold}s, cycle dwell {Dwell}s).",num5,configuration.RotationCoveredHoldSeconds,configuration.RotationCycleDwellSeconds);
			return RotationDecision.None;
		}
		if(!BeastmasterDutyActions.CanUse(46751u)) {
			CoveredStatus="Snarl on cooldown";
			return RotationDecision.None;
		}
		CoveredStatus="Snarling";
		DalamudApi.Log.Information("Snarl pressed: swap prediction was {Until} (hold window {Hold}s).",(num5<0.0)?"none":$"{num5:0.0}s",configuration.RotationCoveredHoldSeconds);
		return Decide("Snarl: keeping Covered up so the familiar takes your damage","Cover",46751u);
	}
	private RotationDecision NextDrakeMorphoChallenge(BeastmasterGaugeSnapshot gauge) {
		if(gauge.SummonDataId==0) {
			CoveredStatus="Drake feed: no familiar out";
			return RotationDecision.None;
		}
		if(!BeastmasterDutyActions.IsAvailable(46750u)) {
			CoveredStatus="Drake feed: Challenge not offered here";
			return RotationDecision.None;
		}
		if(BeastmasterDutyActions.IsCovered()||!HasOwnStatus(5586u)) {
			if(!BeastmasterDutyActions.CanUse(46750u)) {
				CoveredStatus="Drake feed: Challenge on cooldown";
				return RotationDecision.None;
			}
			CoveredStatus="Drake feed: Challenging so Abaddon can reach the Morpho";
			return Decide("Challenge: release Covered so Abaddon can eat the Morpho","Challenge",46750u);
		}
		CoveredStatus="Drake feed: Challenge is up";
		return RotationDecision.None;
	}
	private static IBattleNpc? FamiliarOut() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return null;
		}
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(item is IBattleNpc battleNpc&&battleNpc.OwnerId==localPlayer.EntityId&&battleNpc.MaxHp!=0&&battleNpc.EntityId!=localPlayer.EntityId&&battleNpc.CurrentHp!=0) {
				return battleNpc;
			}
		}
		return null;
	}
	private unsafe RotationDecision NextPlannedEnmity(int level,BeastmasterGaugeSnapshot gauge,ActionManager* actionManager,IBattleChara target) {
		if(gauge.SummonDataId==0) {
			CoveredStatus="No familiar out";
			return RotationDecision.None;
		}
		IBattleNpc battleNpc=FamiliarOut();
		double familiarHp=((battleNpc==null)?100.0:((double)battleNpc.CurrentHp*100.0/(double)battleNpc.MaxHp));
		var (enmityWant,text)=CrucibleEnmityPlan.Decide(configuration,gauge.SummonEntry?.Number,familiarHp,PlayerHpPercent());
		CrucibleEnmityPlan.Note($"{enmityWant}: {text}");
		CoveredStatus=((enmityWant==EnmityWant.Cover)?"Cover":"Challenge")+" - "+text;
		if(enmityWant==EnmityWant.Cover) {
			if(!BeastmasterDutyActions.IsAvailable(46751u)) {
				CoveredStatus="Snarl not offered here";
				return RotationDecision.None;
			}
			if(BeastmasterDutyActions.IsCovered()||!BeastmasterDutyActions.CanUse(46751u)) {
				return RotationDecision.None;
			}
			(uint,int,string,int) tuple2=BestPlanSwap(level,gauge,actionManager,target);
			if(tuple2.Item1!=0) {
				CoveredStatus=$"Holding Snarl - {tuple2.Item3} is about to take over ({tuple2.Item2} of 100)";
				return RotationDecision.None;
			}
			return Decide("Snarl: "+text,"Cover",46751u);
		}
		if(!BeastmasterDutyActions.IsAvailable(46750u)||!BeastmasterDutyActions.CanUse(46750u)) {
			return RotationDecision.None;
		}
		if(BeastmasterDutyActions.IsCovered()||!HasOwnStatus(5586u)) {
			return Decide("Challenge: "+text,"Challenge",46750u);
		}
		return RotationDecision.None;
	}
	private unsafe bool ReplacementReady(ActionManager* actionManager,BeastmasterGaugeSnapshot gauge) {
		int num=gauge.SummonEntry?.Number ?? 0;
		for(int i=0;i<3;i++) {
			BeastmasterCatalogEntry beastmasterCatalogEntry=BeastmasterTeam.SlotEntry(i);
			if((object)beastmasterCatalogEntry!=null&&beastmasterCatalogEntry.Number!=num&&FamiliarHealthOf(beastmasterCatalogEntry.Number)>0.5f&&OffCooldown(actionManager,BeastmasterActions.Battlehorns[i])) {
				return true;
			}
		}
		return false;
	}
	private double SecondsUntilSwap(BeastmasterGaugeSnapshot gauge) {
		if(!RotationToggleOverrides.Get("Cycle",configuration.RotationCycleFamiliars)||configuration.RotationBattlehornSlot!=0||familiarSummonedAt==DateTime.MinValue) {
			return -1.0;
		}
		double totalSeconds=(DateTime.UtcNow-familiarSummonedAt).TotalSeconds;
		double num=Math.Max(0.0,(double)configuration.RotationCycleDwellSeconds-totalSeconds);
		if(num<=(double)configuration.RotationCoveredHoldSeconds&&!CanSpendFamiliar(gauge,out string _,out string _)) {
			return -1.0;
		}
		return num;
	}
	public void ObserveFamiliar(BeastmasterGaugeSnapshot gauge) {
		TrackSummonChanges(gauge);
		if(CrucibleFightPlan.Applies(configuration)) {
			CrucibleFightPlan.NoteBorrowed(BorrowedKinship);
		}
	}
	private void TrackSummonChanges(BeastmasterGaugeSnapshot gauge) {
		if(gauge.SummonDataId!=0&&gauge.SummonDataId==lastSeenSummon) {
			IPlayerCharacter owner=DalamudApi.ObjectTable.LocalPlayer;
			if(owner!=null) {
				IBattleNpc battleNpc=DalamudApi.ObjectTable.OfType<IBattleNpc>().FirstOrDefault((IBattleNpc npc) => npc.OwnerId==owner.GameObjectId&&npc.MaxHp!=0);
				if(battleNpc!=null) {
					lastFamiliarHpPercent=(int)(100L*(long)battleNpc.CurrentHp/battleNpc.MaxHp);
					BeastmasterCatalogEntry summonEntry=gauge.SummonEntry;
					if((object)summonEntry!=null) {
						lastKnownFamiliarHp[summonEntry.Number]=lastFamiliarHpPercent;
					}
				}
			}
		}
		if(gauge.SummonDataId!=lastSeenSummon) {
			if(familiarSummonedAt!=DateTime.MinValue) {
				double totalSeconds=(DateTime.UtcNow-familiarSummonedAt).TotalSeconds;
				DalamudApi.Log.Information("Familiar changed: was out {OutFor:0.0}s (cycle dwell is {Dwell}s), Covered was {Covered}, last seen at {Hp}% HP, Parting Blow {Blow}.",totalSeconds,configuration.RotationCycleDwellSeconds,BeastmasterDutyActions.IsCovered()?"up":"down",lastFamiliarHpPercent,((DateTime.UtcNow-lastPartingBlowAt).TotalSeconds<5.0)?"just pressed":"not pressed");
			}
			lastFamiliarHpPercent=-1;
			lastSeenSummon=gauge.SummonDataId;
			familiarSummonedAt=((gauge.SummonDataId==0)?DateTime.MinValue:DateTime.UtcNow);
		}
	}
	private unsafe RotationDecision NextCycleDismiss(int level,BeastmasterGaugeSnapshot gauge,ActionManager* actionManager,IBattleChara target) {
		bool flag=UtilityHandoffDue(gauge);
		if(!RotationToggleOverrides.Get("Cycle",configuration.RotationCycleFamiliars)&&!flag) {
			SetCycle("Cycling off","Switch Cycle on to have Parting Blow dismiss each familiar so the next can come out.");
			return RotationDecision.None;
		}
		if(configuration.RotationBattlehornSlot!=0) {
			SetCycle($"Battlehorn {configuration.RotationBattlehornSlot} pinned","Cycling needs the Battlehorn selection on Auto - a pinned slot means you have asked for one specific familiar, so there is nothing to cycle to.");
			return RotationDecision.None;
		}
		if(gauge.SummonDataId==0||familiarSummonedAt==DateTime.MinValue) {
			SetCycle("No familiar out","Nothing to dismiss yet - the cycle starts once a familiar has been summoned.");
			return RotationDecision.None;
		}
		if(!BeastmasterActions.IsUnlocked(44891u,level)) {
			int value=BeastmasterActions.UnlockLevel(44891u);
			SetCycle($"Needs level {value}",$"Parting Blow is what dismisses a familiar, and it unlocks at level {value}.");
			return RotationDecision.None;
		}
		int num=CountUsableSlots(level);
		if(num<2) {
			SetCycle($"Only {num} slot","Cycling needs at least two usable Battlehorns - with one there is nowhere to cycle to.");
			return RotationDecision.None;
		}
		if(!CanSpendFamiliar(gauge,out string reason,out string detail)) {
			SetCycle(reason,detail);
			return RotationDecision.None;
		}
		if(!flag&&!ReplacementReady(actionManager,gauge)) {
			SetCycle("No replacement ready","Every other Battlehorn is on cooldown (or its familiar is gone), so the current familiar stays out instead of leaving none.");
			return RotationDecision.None;
		}
		if(!Ready(actionManager,44891u,target)) {
			if(DalamudApi.Condition[ConditionFlag.InCombat]) {
				SetCycle("Blow not ready","Parting Blow is on cooldown, or cannot reach the target from here.");
			}
			else {
				SetCycle("Waiting for combat","Parting Blow can only be used in combat, so the cycle cannot run out of a fight.");
			}
			return RotationDecision.None;
		}
		SetCycle("Cycling now","Parting Blow is going out and the next familiar follows.");
		double totalSeconds=(DateTime.UtcNow-familiarSummonedAt).TotalSeconds;
		string value2=gauge.SummonEntry?.DisplayName ?? "familiar";
		return Decide($"Parting Blow: {value2} has been out {totalSeconds:0}s - cycling to the next familiar","Cycle",44891u);
	}
	private int CountUsableSlots(int level) {
		int num=0;
		int num2=0;
		for(int i=0;i<BeastmasterActions.Battlehorns.Length;i++) {
			if(BeastmasterActions.IsUnlocked(BeastmasterActions.Battlehorns[i],level)) {
				num2++;
				if(BeastmasterTeam.RawSlot(i)!=0) {
					num++;
				}
			}
		}
		if(num<=0) {
			return num2;
		}
		return num;
	}
	private unsafe RotationDecision SwapToPinnedFamiliar(int level,BeastmasterGaugeSnapshot gauge,ActionManager* actionManager,IBattleChara target) {
		int num=Math.Clamp(configuration.RotationBattlehornSlot,0,3);
		if(num==0||gauge.SummonDataId==0||MorphoPhase()) {
			return RotationDecision.None;
		}
		if(PlayerMovement.IsMoving) {
			return RotationDecision.None;
		}
		BeastmasterCatalogEntry beastmasterCatalogEntry=BeastmasterTeam.SlotEntry(num-1);
		if(beastmasterCatalogEntry==null) {
			return RotationDecision.None;
		}
		if(beastmasterCatalogEntry.SummonDataId==gauge.SummonDataId) {
			swapAttempts=0;
			return RotationDecision.None;
		}
		if(swapAttemptsForSlot!=num||swapAttemptsForSummon!=gauge.SummonDataId) {
			swapAttemptsForSlot=num;
			swapAttemptsForSummon=gauge.SummonDataId;
			swapAttempts=0;
		}
		if(swapAttempts>=3) {
			LastReason="Could not swap to "+beastmasterCatalogEntry.DisplayName+" - the horn did not change the familiar";
			return RotationDecision.None;
		}
		uint actionId=BeastmasterActions.Battlehorns[num-1];
		if(!BeastmasterActions.IsUnlocked(actionId,level)) {
			LastReason=$"Battlehorn {num} unlocks at level {BeastmasterActions.UnlockLevel(actionId)}";
			return RotationDecision.None;
		}
		if(!Ready(actionManager,actionId,target)) {
			return RotationDecision.None;
		}
		swapAttempts++;
		string value=gauge.SummonEntry?.DisplayName ?? $"DataId {gauge.SummonDataId}";
		return Decide($"Swapping {value} -> {beastmasterCatalogEntry.DisplayName} (Battlehorn {num})","Summon",actionId);
	}
	private unsafe RotationDecision SwapForPlan(int level,BeastmasterGaugeSnapshot gauge,ActionManager* actionManager,IBattleChara target) {
		if(!CrucibleFightPlan.Applies(configuration)||MorphoPhase()||gauge.SummonDataId==0||PlayerMovement.IsMoving||HasOwnStatus(4601u)||CrucibleFightPlan.WantsOpener(out var _)||(DateTime.UtcNow-CrucibleFightPlan.LastPlanSwap).TotalSeconds<6.0) {
			return RotationDecision.None;
		}
		var (num,num2,text,num3)=BestPlanSwap(level,gauge,actionManager,target);
		if(num==0) {
			planSwapTries=0;
			return RotationDecision.None;
		}
		DalamudApi.Log.Information("Fight plan swap: -> {Name} (need {Need}, current lent kin need {CurrentNeed}).",text,num2,num3);
		if(planSwapFor!=gauge.SummonDataId) {
			planSwapFor=gauge.SummonDataId;
			planSwapTries=0;
		}
		if(planSwapTries>=3) {
			return RotationDecision.None;
		}
		planSwapTries++;
		CrucibleFightPlan.LastPlanSwap=DateTime.UtcNow;
		string value=gauge.SummonEntry?.DisplayName ?? "the familiar";
		return Decide($"Swapping {value} -> {text}: the fight needs its kinship now ({num2} of 100, {BorrowedKinship} lent is {num3})","Summon",num);
	}
	private unsafe (uint Horn,int Need,string Name,int CurrentNeed) BestPlanSwap(int level,BeastmasterGaugeSnapshot gauge,ActionManager* actionManager,IBattleChara target) {
		if(!CrucibleFightPlan.Applies(configuration)||gauge.SummonDataId==0) {
			return (Horn: 0u,Need: 0,Name: string.Empty,CurrentNeed: 0);
		}
		BeastKin beastKin=CrucibleFightPlan.PlanLent(BorrowedKinship);
		int num=CrucibleFightPlan.KinNeed(configuration,beastKin,gauge.SummonEntry?.Number ?? 0);
		uint num2=0u;
		int num3=0;
		string item=string.Empty;
		for(int i=0;i<BeastmasterActions.Battlehorns.Length;i++) {
			BeastmasterCatalogEntry beastmasterCatalogEntry=BeastmasterTeam.SlotEntry(i);
			uint num4=BeastmasterActions.Battlehorns[i];
			if(beastmasterCatalogEntry==null||beastmasterCatalogEntry.SummonDataId==gauge.SummonDataId||!BeastmasterActions.IsUnlocked(num4,level)||!Ready(actionManager,num4,target)) {
				continue;
			}
			BeastKin beastKin2=BeastmasterKinship.KinOf(beastmasterCatalogEntry.Number);
			if(beastKin2!=beastKin&&CrucibleFightPlan.UseFor(configuration,CrucibleFightPlan.ActiveBoard,CrucibleFightPlan.Active,beastmasterCatalogEntry.Number).Use==PlannedUse.Borrow) {
				int num5=CrucibleFightPlan.KinNeed(configuration,beastKin2,beastmasterCatalogEntry.Number);
				if(num5>num3) {
					num3=num5;
					num2=num4;
					item=beastmasterCatalogEntry.DisplayName;
				}
			}
		}
		if(num2==0||num3<85||num3<num+10) {
			return (Horn: 0u,Need: num3,Name: item,CurrentNeed: num);
		}
		return (Horn: num2,Need: num3,Name: item,CurrentNeed: num);
	}
	private unsafe uint ChooseBattlehorn(int level,ActionManager* actionManager,ulong targetId) {
		int num=Math.Clamp(configuration.RotationBattlehornSlot,0,3);
		if(num>0) {
			uint num2=BeastmasterActions.Battlehorns[num-1];
			if(!BeastmasterActions.IsUnlocked(num2,level)) {
				LastReason=$"Battlehorn {num} unlocks at level {BeastmasterActions.UnlockLevel(num2)}";
				return 0u;
			}
			if(!Ready(actionManager,num2,targetId)) {
				return 0u;
			}
			return num2;
		}
		if(CrucibleFightPlan.Applies(configuration)) {
			uint num3=ChooseByPlan(level,actionManager,targetId);
			if(num3!=0) {
				return num3;
			}
			if(holdSummonForOpener||MorphoPhase()) {
				return 0u;
			}
		}
		else {
			DalamudApi.Log.Information("Battlehorn pick: fight plan does not apply (suggested mode {Mode}, active fight {Fight}) - falling back to {Order}.",configuration.CrucibleSuggestedTeamMode,CrucibleFightPlan.Active?.Name ?? "none",configuration.BattlehornOrder);
		}
		if(configuration.BattlehornOrder!=BattlehornOrder.SlotOrder) {
			return ChooseByKinship(level,actionManager,targetId);
		}
		return ChooseBySlotOrder(level,actionManager,targetId);
	}
	private unsafe uint ChooseBySlotOrder(int level,ActionManager* actionManager,ulong targetId) {
		for(int i=0;i<2;i++) {
			bool flag=i==0&&RotationToggleOverrides.Get("Cycle",configuration.RotationCycleFamiliars);
			for(int j=0;j<BeastmasterActions.Battlehorns.Length;j++) {
				uint num=BeastmasterActions.Battlehorns[j];
				if(BeastmasterActions.IsUnlocked(num,level)&&Ready(actionManager,num,targetId)&&(!flag||j!=lastSummonedSlot)) {
					lastSummonedSlot=j;
					BeastmasterCatalogEntry beastmasterCatalogEntry=BeastmasterTeam.SlotEntry(j);
					LastReason=((beastmasterCatalogEntry==null)?$"Summoning from Battlehorn {j+1}":$"Summoning {beastmasterCatalogEntry.DisplayName} (Battlehorn {j+1})");
					return num;
				}
			}
		}
		return 0u;
	}
	private unsafe uint ChooseByPlan(int level,ActionManager* actionManager,ulong targetId) {
		BeastKin beastKin=CrucibleFightPlan.PlanLent(BorrowedKinship);
		uint num=0u;
		int num2=int.MaxValue;
		string text=string.Empty;
		bool flag=CrucibleFightPlan.WantsOpener(out var openerKin);
		string[] array=(from e in Enumerable.Range(0,BeastmasterActions.Battlehorns.Length).Select(BeastmasterTeam.SlotEntry)
			select (!(e==null))?$"{e.DisplayName}={BeastmasterKinship.KinOf(e.Number)}":"(empty)").ToArray();
		bool flag2=flag&&Enumerable.Range(0,BeastmasterActions.Battlehorns.Length).Select(BeastmasterTeam.SlotEntry).Any((BeastmasterCatalogEntry e) => e!=null&&BeastmasterKinship.KinOf(e.Number)==openerKin);
		bool flag3=array.Any((string r) => r!="(empty)");
		if(flag&&!flag2&&!flag3) {
			return 0u;
		}
		if(flag&&!flag2) {
			DalamudApi.Log.Warning("Fight opener given up: wanted {Kin}, roster is [{Roster}] - none of the team carries it.",openerKin,string.Join(", ",array));
			CrucibleFightPlan.GiveUpOnOpener();
		}
		else if(!flag) {
			DalamudApi.Log.Information("ChooseByPlan: no opener pending (fight {Fight}, roster [{Roster}]) - scoring normally.",CrucibleFightPlan.Active?.Name ?? "none",string.Join(", ",array));
		}
		for(int num3=0;num3<BeastmasterActions.Battlehorns.Length;num3++) {
			uint num4=BeastmasterActions.Battlehorns[num3];
			BeastmasterCatalogEntry beastmasterCatalogEntry=BeastmasterTeam.SlotEntry(num3);
			if(beastmasterCatalogEntry!=null&&BeastmasterActions.IsUnlocked(num4,level)&&!Ready(actionManager,num4,targetId)&&CrucibleFightPlan.Active.BorrowKins.Contains(BeastmasterKinship.KinOf(beastmasterCatalogEntry.Number),null)&&(DateTime.UtcNow-refreshSkipLoggedAt).TotalSeconds>=5.0) {
				refreshSkipLoggedAt=DateTime.UtcNow;
				DalamudApi.Log.Information("{Name}'s Battlehorn is not ready, so its Borrow cannot be refreshed yet - picking another familiar.",beastmasterCatalogEntry.DisplayName);
			}
			if(beastmasterCatalogEntry==null||!BeastmasterActions.IsUnlocked(num4,level)||!Ready(actionManager,num4,targetId)) {
				continue;
			}
			BeastKin beastKin2=BeastmasterKinship.KinOf(beastmasterCatalogEntry.Number);
			if(MorphoPhase()&&!CrucibleFightPlan.Active.BorrowKins.Contains(beastKin2,null)) {
				continue;
			}
			if(flag2) {
				if(beastKin2==openerKin) {
					num=num4;
					text=beastmasterCatalogEntry.DisplayName;
					num2=-1;
					break;
				}
				continue;
			}
			bool flag4=CrucibleFightPlan.UseFor(configuration,CrucibleFightPlan.ActiveBoard,CrucibleFightPlan.Active,beastmasterCatalogEntry.Number).Use==PlannedUse.Borrow;
			int num5=((flag4&&beastKin2!=beastKin)?CrucibleFightPlan.KinNeed(configuration,beastKin2,beastmasterCatalogEntry.Number):0);
			if(flag4&&CrucibleFightPlan.Active.BorrowKins.Contains(beastKin2,null)) {
				float num6=BeastmasterKinship.BorrowedRemaining();
				if(beastKin2==beastKin&&num6>=0f&&num6<=32f) {
					num5=Math.Max(num5,88);
					LastReason=$"{beastKin2} has {num6:0}s left - refreshing it before the next familiar";
				}
				else if(beastKin==BeastKin.Unknown) {
					num5=Math.Max(num5,75);
				}
			}
			int num7=((num5>=60)?(100-num5):200);
			if(num3==lastSummonedSlot&&RotationToggleOverrides.Get("Cycle",configuration.RotationCycleFamiliars)) {
				num7++;
			}
			if(num7<num2) {
				num2=num7;
				num=num4;
				text=beastmasterCatalogEntry.DisplayName;
			}
		}
		holdSummonForOpener=false;
		if(flag2&&num==0) {
			DateTime valueOrDefault=openerHoldSince.GetValueOrDefault();
			if(!openerHoldSince.HasValue) {
				valueOrDefault=DateTime.UtcNow;
				openerHoldSince=valueOrDefault;
			}
			if((DateTime.UtcNow-openerHoldSince.Value).TotalSeconds<25.0) {
				holdSummonForOpener=true;
				LastReason="Waiting for the opener familiar's Battlehorn to be ready";
			}
		}
		else if(num!=0) {
			openerHoldSince=null;
		}
		if(num!=0) {
			lastSummonedSlot=Array.IndexOf(BeastmasterActions.Battlehorns,num);
			BeastmasterRotationService beastmasterRotationService=this;
			string lastReason;
			if(flag2) {
				lastReason="Summoning "+text+": the fight's named opener";
			}
			else {
				lastReason=((num2<150)?$"Summoning {text}: the fight plan needs its Borrow ({100-num2} of 100)":("Summoning "+text+" for damage (fight plan)"));
			}
			beastmasterRotationService.LastReason=lastReason;
		}
		return num;
	}
	private unsafe uint ChooseByKinship(int level,ActionManager* actionManager,ulong targetId) {
		bool lowHp=configuration.KinshipMitigationWhenLow&&PlayerHpPercent()<configuration.KinshipMitigationHpThreshold;
		uint num=PickBestSlot(level,actionManager,targetId,lowHp,RotationToggleOverrides.Get("Cycle",configuration.RotationCycleFamiliars));
		if(num==0&&RotationToggleOverrides.Get("Cycle",configuration.RotationCycleFamiliars)) {
			num=PickBestSlot(level,actionManager,targetId,lowHp,skipLastSummoned: false);
		}
		if(num!=0) {
			lastSummonedSlot=Array.IndexOf(BeastmasterActions.Battlehorns,num);
			return num;
		}
		uint[] battlehorns=BeastmasterActions.Battlehorns;
		foreach(uint num2 in battlehorns) {
			if(BeastmasterActions.IsUnlocked(num2,level)&&Ready(actionManager,num2,targetId)) {
				return num2;
			}
		}
		return 0u;
	}
	private unsafe uint PickBestSlot(int level,ActionManager* actionManager,ulong targetId,bool lowHp,bool skipLastSummoned) {
		List<int> kinshipPriority=configuration.KinshipPriority;
		uint num=0u;
		int num2=int.MaxValue;
		BeastKin beastKin=BeastKin.Unknown;
		for(int i=0;i<BeastmasterActions.Battlehorns.Length;i++) {
			uint num3=BeastmasterActions.Battlehorns[i];
			if(!BeastmasterActions.IsUnlocked(num3,level)||!Ready(actionManager,num3,targetId)||(skipLastSummoned&&i==lastSummonedSlot&&!lowHp)) {
				continue;
			}
			BeastmasterCatalogEntry beastmasterCatalogEntry=BeastmasterTeam.SlotEntry(i);
			if(beastmasterCatalogEntry==null) {
				continue;
			}
			BeastKin beastKin2=BeastmasterKinship.KinOf(beastmasterCatalogEntry.Number);
			if(beastKin2!=BeastKin.Unknown) {
				int num4=kinshipPriority.IndexOf((int)beastKin2);
				if(num4<0) {
					num4=kinshipPriority.Count;
				}
				if(lowHp&&BeastmasterKinship.IsMitigation(beastKin2)) {
					num4-=kinshipPriority.Count;
				}
				if(num4<num2) {
					num2=num4;
					num=num3;
					beastKin=beastKin2;
				}
			}
		}
		if(num!=0) {
			LastReason=((lowHp&&BeastmasterKinship.IsMitigation(beastKin))?$"Summoning {beastKin} for mitigation - HP is low":$"Summoning {beastKin}: {BeastmasterKinship.Effect(beastKin)}");
		}
		return num;
	}
	private static float PlayerHpPercent() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null||localPlayer.MaxHp==0) {
			return 100f;
		}
		return (float)localPlayer.CurrentHp*100f/(float)localPlayer.MaxHp;
	}
	private static InstinctAffinity ReadHeldHeart() {
		return BeastmasterStatusReader.HeldHeart();
	}
	private static bool HasOwnStatus(uint statusId) {
		return BeastmasterStatusReader.Has(statusId);
	}
	private static int CountEnemiesNear(IBattleChara target,float radius=8f) {
		int num=0;
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(item is IBattleNpc { ObjectKind: ObjectKind.BattleNpc,IsDead: false,CurrentHp: not 0u,IsTargetable: not false } battleNpc&&Vector3.Distance(battleNpc.Position,target.Position)<=radius) {
				num++;
			}
		}
		return num;
	}
	private unsafe static bool OffCooldown(ActionManager* actionManager,uint actionId) {
		float recastTime=actionManager->GetRecastTime(ActionType.Action,actionId);
		float recastTimeElapsed=actionManager->GetRecastTimeElapsed(ActionType.Action,actionId);
		return recastTime-recastTimeElapsed<=0.5f;
	}
	private bool AutoSummonAllowed() {
		if(RotationToggleOverrides.Get("AutoSummon",configuration.RotationAutoSummon)) {
			return DateTime.UtcNow>=summonGuardUntil;
		}
		return false;
	}
	private unsafe bool Ready(ActionManager* actionManager,uint actionId,IBattleChara target) {
		return Ready(actionManager,actionId,SelfOr(target.GameObjectId,actionId));
	}
	public static ulong SelfOr(ulong targetId,uint actionId) {
		if(!BeastmasterActions.IsSelfTargeted(actionId)) {
			return targetId;
		}
		return DalamudApi.ObjectTable.LocalPlayer?.GameObjectId ?? targetId;
	}
	private unsafe bool Ready(ActionManager* actionManager,uint actionId,ulong targetId) {
		if(!RotationToggleOverrides.AllowsAction(configuration,actionId)) {
			return false;
		}
		if(BeastmasterActionHolds.IsHeld(actionId)) {
			return false;
		}
		if(BeastmasterDutyActions.IsDutyAction(actionId)) {
			return BeastmasterDutyActions.CanUse(actionId);
		}
		if(actionManager->GetActionStatus(ActionType.Action,actionId,targetId,checkRecastActive: true,checkCastingActive: true,null)==0) {
			return true;
		}
		uint adjustedActionId=actionManager->GetAdjustedActionId(actionId);
		if(adjustedActionId!=actionId) {
			return actionManager->GetActionStatus(ActionType.Action,adjustedActionId,targetId,checkRecastActive: true,checkCastingActive: true,null)==0;
		}
		return false;
	}
	private RotationDecision Decide(string reason,string phase,uint actionId) {
		LastReason=reason;
		Phase=phase;
		if(actionId==44891) {
			lastPartingBlowAt=DateTime.UtcNow;
		}
		if(actionId==47093) {
			lastTrickDecidedAt=DateTime.UtcNow;
		}
		bool flag;
		switch(phase) {
		case "Wheel":
		case "Cooldown":
		case "Cycle":
		case "Opener":
		flag=true;
		break;
		default:
		flag=false;
		break;
		}
		if(flag) {
			Trace(actionId,reason);
		}
		return new RotationDecision(actionId,reason,phase);
	}
	private void Trace(uint actionId,string reason) {
		try {
			BeastmasterGaugeSnapshot beastmasterGaugeSnapshot=traceGauge;
			string value=DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(actionId)?.Name.ToString() ?? actionId.ToString();
			string text=$"{value} | {reason} | TP {beastmasterGaugeSnapshot?.Tp} power {beastmasterGaugeSnapshot?.BeastPower} instinct 0x{beastmasterGaugeSnapshot?.BeastHeart:X2} (mastered {beastmasterGaugeSnapshot?.BeastHeartStacks}, natural {beastmasterGaugeSnapshot?.BeastSoulStacks}, sane {beastmasterGaugeSnapshot?.StacksLookSane}) | heart {HeldHeart} half {HeldInfinitiveHalf} burning {Burning} (toggle {RotationToggleOverrides.Get("Burn",configuration.RotationBurn)}, lowHp {BelowBurnHp}, add {AddBurstTiming.Burst}) lvl {traceLevel} target {targetHpPercent:0}%";
			if(!(text==lastTraceLine)||!((DateTime.UtcNow-lastTraceAt).TotalSeconds<2.0)) {
				lastTraceLine=text;
				lastTraceAt=DateTime.UtcNow;
				string text2=Path.Combine(DalamudApi.PluginInterface.GetPluginConfigDirectory(),"rotation-trace.log");
				if(File.Exists(text2)&&new FileInfo(text2).Length>20000000) {
					File.Move(text2,text2+".old",overwrite: true);
				}
				File.AppendAllText(text2,$"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {text}{Environment.NewLine}");
			}
		}
		catch {
		}
	}
}
