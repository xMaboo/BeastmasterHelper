using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;
namespace Beastmaster;

[Serializable]
public sealed class BeastmasterConfiguration : IPluginConfiguration {
	[NonSerialized]
	private IDalamudPluginInterface? pluginInterface;
	public bool RotationUseOpener=true;
	public bool RotationUseAoe=true;
	public bool RotationUseCooldowns=true;
	public bool RotationUseRally=true;
	public bool RotationUseRallyingCheer=true;
	public bool RotationUsePowerActions=true;
	public bool RotationBurn;
	public float RotationBurnBelowEnemyHpPercent;
	public bool RotationAutoAttack=true;
	public bool RotationAllowPartingBlow;
	public bool ShieldChargeGapClose=true;
	public List<string> ShieldChargeBlockedEncounters=new List<string>();
	public bool ShieldChargeAoe;
	public bool RotationAutoSummon=true;
	public bool RotationCycleFamiliars;
	public bool RotationKeepCovered;
	public bool RotationCycleWaitForSpentPower=true;
	public bool KinshipMitigationWhenLow=true;
	public bool KinshipMitigationOnCooldown=true;
	public bool RotationTrickWaitForTp=true;
	public bool RotationPoolForBurst=true;
	public bool RotationUseOneWithNature=true;
	public bool RotationUseBeastMode=true;
	public bool BeastModeQuellingWaveInMelee;
	public bool BeastModeAllowCloudSkim;
	public bool RotationPartingBlowNeedsVantage=true;
	public bool ShowOverlay=true;
	public bool LockOverlay;
	public bool AutoHideOverlay=true;
	public bool ShowRotationOverlay;
	public bool ArenaOverlayEnabled;
	public bool ArenaShowCentre=true;
	public int Version { get; set; }=10;
	public string SelectedStageKey { get; set; }=string.Empty;
	public bool CrucibleBoardAssist { get; set; }
	public bool CrucibleAutoAdvance { get; set; }
	public int CrucibleEventGraceSeconds { get; set; }=8;
	public bool CrucibleCampsiteWhenHurt { get; set; }=true;
	public bool CrucibleCheckTeamBeforeFork { get; set; }=true;
	public int CrucibleCampsiteTotalMissing { get; set; }=120;
	public float CrucibleCampsiteHpPercent { get; set; }=60f;
	public float CrucibleCampsiteFamiliarHpPercent { get; set; }=60f;
	public int CrucibleCampsiteHurtFamiliars { get; set; }=2;
	public bool CrucibleTakeAllLoot { get; set; }=true;
	public bool CrucibleAutoTreasure { get; set; }=true;
	public string CrucibleRecordedClick { get; set; }=string.Empty;
	public bool CrucibleAutoHeal { get; set; }=true;
	public int CrucibleHealingStock { get; set; }=3;
	public int CruciblePlayerBuyLimit { get; set; }=2;
	public int CrucibleFamiliarBuffTarget { get; set; }=4;
	public float CrucibleFeedMinHpPercent { get; set; }=50f;
	public bool CrucibleUseTomeInFight { get; set; }=true;
	public float CrucibleAutoPotionHpPercent { get; set; }=50f;
	public int CruciblePlayerPotionReserve { get; set; }=3;
	public float CrucibleDesperateHpPercent { get; set; }=35f;
	public bool CrucibleAutoPrepare { get; set; }=true;
	public bool CrucibleRepairEnabled { get; set; }=true;
	public int CrucibleRepairThreshold { get; set; }=50;
	public uint CrucibleFoodItemId { get; set; }
	public int CrucibleFoodMinMinutes { get; set; }=25;
	public int CrucibleBoard { get; set; }
	public int CrucibleDifficulty { get; set; }
	public bool CrucibleAutoConfigureBmrMovement { get; set; }=true;
	public bool CrucibleSmartEnmity { get; set; }=true;
	public int CrucibleEnmityEmergencyPercent { get; set; }=10;
	public int CrucibleEnmityHurtPercent { get; set; }=30;
	public int CrucibleEnmityShareFloor { get; set; }=70;
	public bool CrucibleDifficultySeen { get; set; }
	public bool CrucibleDifficultyRightIsHarder { get; set; }=true;
	public bool CrucibleRecommendedTeam { get; set; }=true;
	public bool CrucibleSuggestedTeamMode { get; set; }
	public bool CrucibleGuideUseEntryPool { get; set; }=true;
	public bool CrucibleGuideUseFightTeams { get; set; }=true;
	public Dictionary<string,int[]> CrucibleFightTeamOverrides { get; set; }=new Dictionary<string,int[]>();
	public Dictionary<string,int> CrucibleFightUseOverrides { get; set; }=new Dictionary<string,int>();
	public HashSet<string> CrucibleReactionHelperFights { get; set; }=new HashSet<string>();
	public bool CrucibleLoopSelectedBoard { get; set; }
	public bool CrucibleLevelUpBeasts { get; set; }
	public int CrucibleLevelRescanEvery { get; set; }=10;
	public int CrucibleGridBufferMs { get; set; }=50;
	public int CrucibleLevelRunsSinceScan { get; set; }
	public Dictionary<uint,int> CrucibleBeastRankLearned { get; set; }=new Dictionary<uint,int>();
	public Dictionary<uint,int> CrucibleBeastExpLearned { get; set; }=new Dictionary<uint,int>();
	public Dictionary<uint,string> CrucibleBeastExpRaw { get; set; }=new Dictionary<uint,string>();
	public Dictionary<int,int> CrucibleBeastRankFallback { get; set; }=new Dictionary<int,int>();
	public int CrucibleLevelUpRankThreshold { get; set; }=10;
	public Dictionary<uint,bool> CrucibleBeastRankLevelingSelected { get; set; }=new Dictionary<uint,bool>();
	public string SelectedMainSection { get; set; }="quests";
	public bool HideCompletedQuests { get; set; }
	public bool SortCatalogByLocation { get; set; }
	public bool SortCatalogByLevel { get; set; }
	public bool HideCapturedBeasts { get; set; }
	public bool AutoCompleteCatalogFromChat { get; set; }=true;
	public bool UseFlightNavigation { get; set; }=true;
	public bool SetFlagOnNavigation { get; set; }=true;
	public bool ShowNavigationLogs { get; set; }=true;
	public bool AutoCaptureEnabled { get; set; }
	public bool AutoCaptureTryCapture { get; set; }=true;
	public float CaptureHpThreshold { get; set; }=80f;
	public bool ShowGaugeInOverlay { get; set; }
	public bool AdvancedActionsEnabled { get; set; }
	public bool BeastHeartCooperationEnabled { get; set; }
	public bool BeastSoulCooperationEnabled { get; set; }
	public bool AutoReleaseEnabled { get; set; }=true;
	public bool RotationEnabled { get; set; }
	public int RotationAoeThreshold { get; set; }=3;
	public int UpdateIntervalMs { get; set; }=5;
	public float ShieldChargeMinDistance { get; set; }=6f;
	public int ShieldChargeAoeCount { get; set; }=3;
	public float ShieldChargeAoeRadius { get; set; }=6f;
	public int RotationCycleDwellSeconds { get; set; }=45;
	public int RotationCoveredHoldSeconds { get; set; }=16;
	public int RotationCoveredFamiliarHpFloor { get; set; }=15;
	public int RotationCoveredReleaseHp { get; set; }=35;
	public int RotationBattlehornSlot { get; set; }
	public BattlehornOrder BattlehornOrder { get; set; }
	public List<int> KinshipPriority { get; set; }=BeastmasterKinship.DefaultPriority();
	public float KinshipMitigationHpThreshold { get; set; }=50f;
	public int RotationAxeTpThreshold { get; set; }=200;
	public int RotationRallyMinStacks { get; set; }=3;
	public int RotationCheerMinStacks { get; set; }=3;
	public int RotationRallyStallSeconds { get; set; }=20;
	public int UniversalityCombosLanded { get; set; }
	public int RotationUniversalityHoldSeconds { get; set; }
	public OneWithNatureChoice OneWithNature { get; set; }
	public float OverlayBgAlpha { get; set; }=0.85f;
	public float OverlayWidth { get; set; }=235f;
	public float RotationOverlayIconSize { get; set; }=32f;
	public Dictionary<string,OverlayPanelSettings> RotationPanelLayout { get; set; }=new Dictionary<string,OverlayPanelSettings>();
	public List<string> QuickToggleOrder { get; set; }=new List<string>();
	public Dictionary<string,QuickToggleBinding> QuickToggleBindings { get; set; }=new Dictionary<string,QuickToggleBinding>();
	public List<string> HotbarOrder { get; set; }=new List<string>();
	public Dictionary<string,QuickToggleBinding> HotbarBindings { get; set; }=new Dictionary<string,QuickToggleBinding>();
	public int QtHSpacing { get; set; }=5;
	public int QtVSpacing { get; set; }=5;
	public int QtButtonHeight { get; set; }
	public float QtTextScale { get; set; }=1f;
	public Vector4 QtEnabled { get; set; }=new Vector4(0.24f,0.52f,0.26f,0.95f);
	public Vector4 QtDisabled { get; set; }=new Vector4(0.17f,0.18f,0.19f,0.95f);
	public ArenaShape ArenaShape { get; set; }
	public ArenaCentreMode ArenaCentreMode { get; set; }
	public float ArenaSize { get; set; }=20f;
	public float ArenaSizeZ { get; set; }=20f;
	public float ArenaRotationDegrees { get; set; }
	public float ArenaThickness { get; set; }=2.5f;
	public float ArenaCentreX { get; set; }
	public float ArenaCentreY { get; set; }
	public float ArenaCentreZ { get; set; }
	public ushort ArenaCentreTerritory { get; set; }
	public float ArenaColourR { get; set; }=1f;
	public float ArenaColourG { get; set; }=0.85f;
	public float ArenaColourB { get; set; }=0.3f;
	public float ArenaColourA { get; set; }=0.9f;
	public Dictionary<string,BeastmasterCharacterProgress> ProgressByCharacter { get; set; }=new Dictionary<string,BeastmasterCharacterProgress>(StringComparer.Ordinal);
	public bool NatureChosenByHand { get; set; }
	public QuickToggleBinding ToggleBinding(string key,string defaultPanel) {
		if(QuickToggleBindings==null) {
			Dictionary<string,QuickToggleBinding> dictionary=(QuickToggleBindings=new Dictionary<string,QuickToggleBinding>());
		}
		if(!QuickToggleBindings.TryGetValue(key,out QuickToggleBinding value)||value==null) {
			value=new QuickToggleBinding {
				Panel = defaultPanel
			};
			QuickToggleBindings[key]=value;
		}
		if(string.IsNullOrEmpty(value.Panel)) {
			value.Panel=defaultPanel;
		}
		return value;
	}
	public QuickToggleBinding HotbarBinding(uint actionId) {
		if(HotbarBindings==null) {
			Dictionary<string,QuickToggleBinding> dictionary=(HotbarBindings=new Dictionary<string,QuickToggleBinding>());
		}
		string key=actionId.ToString();
		if(!HotbarBindings.TryGetValue(key,out QuickToggleBinding value)||value==null) {
			value=new QuickToggleBinding();
			HotbarBindings[key]=value;
		}
		return value;
	}
	public static List<string> Arrange(IReadOnlyList<string>? order,IEnumerable<string> defaults) {
		List<string> list=defaults.ToList();
		List<string> result=(order ?? Array.Empty<string>()).Where(list.Contains).Distinct().ToList();
		result.AddRange(list.Where((string k) => !result.Contains(k)));
		return result;
	}
	public OverlayPanelSettings Panel(string key) {
		if(RotationPanelLayout==null) {
			Dictionary<string,OverlayPanelSettings> dictionary=(RotationPanelLayout=new Dictionary<string,OverlayPanelSettings>());
		}
		if(RotationPanelLayout.TryGetValue(key,out OverlayPanelSettings value)&&value!=null) {
			value.Clamp();
			return value;
		}
		OverlayPanelSettings overlayPanelSettings=RotationPanels.Defaults(key);
		RotationPanelLayout[key]=overlayPanelSettings;
		return overlayPanelSettings;
	}
	public void Initialize(IDalamudPluginInterface pluginInterface) {
		this.pluginInterface=pluginInterface;
		if(ProgressByCharacter==null) {
			Dictionary<string,BeastmasterCharacterProgress> dictionary=(ProgressByCharacter=new Dictionary<string,BeastmasterCharacterProgress>(StringComparer.Ordinal));
		}
		if(Version<9) {
			CaptureHpThreshold=Math.Clamp(CaptureHpThreshold,1f,100f);
			RotationAoeThreshold=Math.Clamp(RotationAoeThreshold,2,10);
			RotationAxeTpThreshold=Math.Clamp(RotationAxeTpThreshold,100,250);
			RotationRallyMinStacks=Math.Clamp(RotationRallyMinStacks,1,3);
			if(RotationPanelLayout==null) {
				Dictionary<string,OverlayPanelSettings> dictionary3=(RotationPanelLayout=new Dictionary<string,OverlayPanelSettings>());
			}
			foreach(OverlayPanelSettings value in RotationPanelLayout.Values) {
				value?.Clamp();
			}
			RotationOverlayIconSize=Math.Clamp(RotationOverlayIconSize,20f,64f);
			RotationCheerMinStacks=Math.Clamp(RotationCheerMinStacks,1,3);
			RotationRallyStallSeconds=Math.Clamp(RotationRallyStallSeconds,0,90);
			CrucibleAutoPotionHpPercent=Math.Clamp(CrucibleAutoPotionHpPercent,1f,100f);
			CruciblePlayerPotionReserve=Math.Clamp(CruciblePlayerPotionReserve,0,10);
			CrucibleDesperateHpPercent=Math.Clamp(CrucibleDesperateHpPercent,1f,100f);
			CrucibleHealingStock=Math.Clamp(CrucibleHealingStock,0,10);
			CruciblePlayerBuyLimit=Math.Clamp(CruciblePlayerBuyLimit,1,2);
			CrucibleFamiliarBuffTarget=Math.Clamp(CrucibleFamiliarBuffTarget,2,6);
			CrucibleFeedMinHpPercent=Math.Clamp(CrucibleFeedMinHpPercent,0f,100f);
			CrucibleEventGraceSeconds=Math.Clamp(CrucibleEventGraceSeconds,3,60);
			CrucibleCampsiteHpPercent=Math.Clamp(CrucibleCampsiteHpPercent,1f,100f);
			CrucibleCampsiteFamiliarHpPercent=Math.Clamp(CrucibleCampsiteFamiliarHpPercent,1f,100f);
			CrucibleCampsiteHurtFamiliars=Math.Clamp(CrucibleCampsiteHurtFamiliars,1,3);
			RotationBattlehornSlot=Math.Clamp(RotationBattlehornSlot,0,3);
			KinshipMitigationHpThreshold=Math.Clamp(KinshipMitigationHpThreshold,1f,100f);
			ShieldChargeMinDistance=Math.Clamp(ShieldChargeMinDistance,3f,20f);
			ShieldChargeAoeCount=Math.Clamp(ShieldChargeAoeCount,2,12);
			ShieldChargeAoeRadius=Math.Clamp(ShieldChargeAoeRadius,3f,15f);
			RotationCycleDwellSeconds=Math.Clamp(RotationCycleDwellSeconds,10,180);
			RotationCoveredHoldSeconds=Math.Clamp(RotationCoveredHoldSeconds,0,60);
			RotationCoveredFamiliarHpFloor=Math.Clamp(RotationCoveredFamiliarHpFloor,0,50);
			RotationCoveredReleaseHp=Math.Clamp(RotationCoveredReleaseHp,0,80);
			ArenaSize=Math.Clamp(ArenaSize,1f,200f);
			ArenaSizeZ=Math.Clamp(ArenaSizeZ,1f,200f);
			ArenaThickness=Math.Clamp(ArenaThickness,0.5f,10f);
			NormaliseKinshipPriority();
			Version=9;
			Save();
		}
		if(Version<10) {
			RotationCycleDwellSeconds=45;
			Version=10;
			Save();
		}
		if(Version<11) {
			if(RotationCoveredHoldSeconds==10) {
				RotationCoveredHoldSeconds=16;
			}
			Version=11;
			Save();
		}
		if(Version<12) {
			CrucibleAutoConfigureBmrMovement=true;
			Version=12;
			Save();
		}
		if(Version<13) {
			if(Math.Abs(OverlayWidth-280f)<0.5f) {
				OverlayWidth=235f;
			}
			Version=13;
			Save();
		}
		if(Version<14) {
			if(Math.Abs(CrucibleAutoPotionHpPercent-40f)<0.5f) {
				CrucibleAutoPotionHpPercent=50f;
			}
			Version=14;
			Save();
		}
		if(Version<15) {
			if(UpdateIntervalMs==100) {
				UpdateIntervalMs=5;
			}
			Version=15;
			Save();
		}
	}
	public void ApplyRecommendedDps() {
		RotationUseOpener=true;
		RotationUseAoe=true;
		RotationAoeThreshold=3;
		RotationUseCooldowns=true;
		RotationUseRally=true;
		RotationUseRallyingCheer=true;
		RotationUsePowerActions=true;
		RotationBurn=false;
		RotationAxeTpThreshold=100;
		RotationTrickWaitForTp=true;
		RotationRallyMinStacks=3;
		RotationCheerMinStacks=3;
		RotationRallyStallSeconds=20;
		RotationUniversalityHoldSeconds=0;
		RotationUseOneWithNature=true;
		OneWithNature=OneWithNatureChoice.TemperedRelease;
		RotationAllowPartingBlow=true;
		RotationPartingBlowNeedsVantage=true;
		RotationCycleFamiliars=true;
		RotationCycleDwellSeconds=45;
		RotationCycleWaitForSpentPower=true;
		RotationAutoSummon=true;
		RotationBattlehornSlot=0;
		BattlehornOrder=BattlehornOrder.SlotOrder;
		ShieldChargeGapClose=true;
		ShieldChargeMinDistance=6f;
		ShieldChargeAoe=false;
		RotationUseBeastMode=true;
		BeastModeQuellingWaveInMelee=false;
		BeastModeAllowCloudSkim=false;
		KinshipMitigationWhenLow=true;
		KinshipMitigationHpThreshold=50f;
		Save();
	}
	public void ResetCrucibleBoardOptionsToGuide() {
		CrucibleFightTeamOverrides.Clear();
		CrucibleFightUseOverrides.Clear();
		Save();
	}
	public void NormaliseKinshipPriority() {
		if(KinshipPriority==null) {
			List<int> list=(KinshipPriority=new List<int>());
		}
		HashSet<int> hashSet=new HashSet<int>();
		List<int> list3=new List<int>(BeastmasterKinship.All.Length);
		foreach(int item2 in KinshipPriority) {
			if(item2>=1&&item2<=8&&hashSet.Add(item2)) {
				list3.Add(item2);
			}
		}
		BeastKin[] all=BeastmasterKinship.All;
		foreach(BeastKin item in all) {
			if(hashSet.Add((int)item)) {
				list3.Add((int)item);
			}
		}
		KinshipPriority=list3;
	}
	public void SetNatureToggle(OneWithNatureChoice choice,bool on) {
		if(on) {
			OneWithNature=choice;
			RotationUseOneWithNature=true;
			NatureChosenByHand=true;
		}
		else if(OneWithNature==choice) {
			RotationUseOneWithNature=false;
		}
	}
	public void Save() {
		pluginInterface?.SavePluginConfig(this);
	}
}
