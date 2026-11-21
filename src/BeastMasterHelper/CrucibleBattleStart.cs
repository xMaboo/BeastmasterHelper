using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
namespace Beastmaster;

internal sealed class CrucibleBattleStart {
	private readonly BeastmasterConfiguration configuration;
	private bool pending;
	private bool sawLoading;
	private bool observed;
	private Vector3 origin;
	private uint territory;
	private DateTime deadline;
	private DateTime settledAt;
	public string Status { get; private set; }="";
	public CrucibleBattleStart(BeastmasterConfiguration configuration) {
		this.configuration=configuration;
	}
	private bool WantSnarl(int? familiarNumber,double familiarHp,double playerHp) {
		if(!CrucibleEnmityPlan.Applies(configuration)) {
			return RotationToggleOverrides.Get("KeepCovered",configuration.RotationKeepCovered);
		}
		return CrucibleEnmityPlan.Decide(configuration,familiarNumber,familiarHp,playerHp).Want==EnmityWant.Cover;
	}
	private static double LiveFamiliarHpPercent(ulong ownerEntityId) {
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(item is IBattleNpc battleNpc&&battleNpc.OwnerId==ownerEntityId&&battleNpc.MaxHp!=0&&battleNpc.EntityId!=ownerEntityId&&battleNpc.CurrentHp!=0) {
				return (double)battleNpc.CurrentHp*100.0/(double)battleNpc.MaxHp;
			}
		}
		return 100.0;
	}
	public void Cancel() {
		pending=false;
	}
	public bool Request() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return false;
		}
		Vector3 position=localPlayer.Position;
		uint territoryType=DalamudApi.ClientState.TerritoryType;
		if(!CrucibleFightReader.Commence()) {
			return false;
		}
		origin=position;
		territory=territoryType;
		sawLoading=false;
		settledAt=DateTime.MinValue;
		deadline=DateTime.UtcNow.AddSeconds(90.0);
		pending=true;
		observed=false;
		Status="Waiting for the fight transition and summoned familiar before the enmity opener.";
		return true;
	}
	public bool ArmFromObservedStart() {
		if(pending) {
			return false;
		}
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return false;
		}
		origin=localPlayer.Position;
		territory=DalamudApi.ClientState.TerritoryType;
		sawLoading=false;
		settledAt=DateTime.MinValue;
		deadline=DateTime.UtcNow.AddSeconds(90.0);
		pending=true;
		observed=true;
		Status="You started the fight - waiting for combat and your familiar before the enmity opener.";
		return true;
	}
	public bool Tick() {
		if(!pending) {
			return false;
		}
		if(DateTime.UtcNow>deadline||!DalamudApi.ClientState.IsLoggedIn||!DalamudApi.Condition[ConditionFlag.BoundByDuty]) {
			pending=false;
			Status="Enmity opener cancelled: duty ended or transition timed out.";
			return false;
		}
		bool flag=DalamudApi.Condition[ConditionFlag.BetweenAreas]||DalamudApi.Condition[ConditionFlag.BetweenAreas51];
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(flag||localPlayer==null) {
			sawLoading|=flag;
			settledAt=DateTime.MinValue;
			return true;
		}
		if(DalamudApi.PlayerState.ClassJob.RowId!=43) {
			pending=false;
			Status="Enmity opener cancelled: no longer Beastmaster.";
			return false;
		}
		if((!sawLoading&&territory==DalamudApi.ClientState.TerritoryType&&!(Vector3.DistanceSquared(origin,localPlayer.Position)>=64f))||CrucibleFightReader.WindowsOpen) {
			settledAt=DateTime.MinValue;
			return true;
		}
		if(settledAt==DateTime.MinValue) {
			settledAt=DateTime.UtcNow;
		}
		if(DateTime.UtcNow-settledAt<TimeSpan.FromSeconds(1L)) {
			return true;
		}
		if(observed&&!DalamudApi.Condition[ConditionFlag.InCombat]) {
			Status="You started the fight - waiting for combat before the enmity opener.";
			return true;
		}
		BeastmasterGaugeSnapshot beastmasterGaugeSnapshot=BeastmasterGaugeSnapshot.Read();
		if(beastmasterGaugeSnapshot.SummonDataId==0) {
			Status="Fight transition complete; waiting for your familiar before the enmity opener.";
			return true;
		}
		double familiarHp=LiveFamiliarHpPercent(localPlayer.EntityId);
		double playerHp=((localPlayer.MaxHp!=0)?((double)localPlayer.CurrentHp*100.0/(double)localPlayer.MaxHp):100.0);
		bool flag2=WantSnarl(beastmasterGaugeSnapshot.SummonEntry?.Number,familiarHp,playerHp);
		uint actionId=(flag2?46751u:46750u);
		string text=(flag2?"Snarl":"Challenge");
		if(!BeastmasterDutyActions.CanUse(actionId)) {
			Status="Familiar present; waiting for "+text+" to become available.";
			return true;
		}
		pending=false;
		Status=(BeastmasterDutyActions.Execute(actionId)?(text+" sent through the duty-action slot. Battle opener complete."):(text+" was not accepted; stopped without repeating it."));
		return false;
	}
}
