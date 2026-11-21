using System;
using Dalamud.Game.ClientState.Conditions;
namespace Beastmaster;

public static class CrucibleEnmityPlan {
	private static bool emergency;
	private static bool playerHurt;
	private static DateTime busterUntil=DateTime.MinValue;
	public static string LastWhy { get; private set; }=string.Empty;
	public static bool Applies(BeastmasterConfiguration configuration) {
		if(configuration.CrucibleSuggestedTeamMode) {
			return DalamudApi.Condition[ConditionFlag.BoundByDuty];
		}
		return false;
	}
	private static bool HasFightPlan(BeastmasterConfiguration configuration) {
		return CrucibleFightPlan.Applies(configuration);
	}
	public static void Reset() {
		emergency=false;
		playerHurt=false;
		busterUntil=DateTime.MinValue;
	}
	private static bool BusterWindow(BeastmasterConfiguration configuration) {
		if(!HasFightPlan(configuration)) {
			return false;
		}
		EnemyCast enemyCast=CrucibleFightPlan.Observe(configuration);
		if((object)enemyCast!=null&&enemyCast.IsBuster) {
			busterUntil=DateTime.UtcNow.AddSeconds((double)Math.Max(1.5f,enemyCast.Remaining)+1.5);
		}
		if(!(DateTime.UtcNow<busterUntil)) {
			return CrucibleFightPlan.BusterWithin(8.0);
		}
		return true;
	}
	public static (EnmityWant Want,string Why) Decide(BeastmasterConfiguration configuration,int? familiar,double familiarHp,double playerHp) {
		int num=Math.Clamp(configuration.CrucibleEnmityEmergencyPercent,1,40);
		if(playerHp<=(double)num) {
			emergency=true;
		}
		else if(playerHp>=50.0) {
			emergency=false;
		}
		if(emergency) {
			return (Want: EnmityWant.Cover,Why: $"you are at {playerHp:0}% - the familiar covers you until you are back above 50%");
		}
		if(familiar.HasValue) {
			int valueOrDefault=familiar.GetValueOrDefault();
			CrucibleGuideFight active=CrucibleFightPlan.Active;
			if((object)active!=null&&active.BorrowKins.Contains(BeastmasterKinship.KinOf(valueOrDefault),null)&&playerHp>=40.0) {
				return (Want: EnmityWant.Challenge,Why: $"the familiar is here for its kinship, not to tank - you hold it (you are at {playerHp:0}%; it covers you below 40%)");
			}
		}
		if(BusterWindow(configuration)) {
			int num2;
			if(familiar.HasValue) {
				int valueOrDefault2=familiar.GetValueOrDefault();
				num2=(FamiliarUtility.IsTank(valueOrDefault2)?1:0);
			}
			else {
				num2=0;
			}
			bool flag=(byte)num2!=0;
			bool flag2=CrucibleFightPlan.MitigationReady();
			if(((!flag2&&playerHp<40.0)&flag)&&familiarHp>40.0) {
				return (Want: EnmityWant.Cover,Why: $"tank buster, no mitigation lent and you are at {playerHp:0}% - the tank familiar takes it");
			}
			return (Want: EnmityWant.Challenge,Why: flag2?"tank buster: you take it with the mitigation you have lent":"tank buster: you take it");
		}
		int num3=Math.Clamp(configuration.CrucibleEnmityHurtPercent,1,60);
		if(playerHp<=(double)num3) {
			playerHurt=true;
		}
		else if(playerHp>=60.0) {
			playerHurt=false;
		}
		int num4=Math.Clamp(configuration.CrucibleEnmityShareFloor,5,95);
		if(familiarHp<=(double)num4) {
			if(playerHurt&&familiarHp>15.0) {
				return (Want: EnmityWant.Cover,Why: $"you are at {playerHp:0}% and the familiar still has {familiarHp:0}% - it keeps covering you");
			}
			return (Want: EnmityWant.Challenge,Why: $"the familiar has taken its share ({familiarHp:0}%, floor is {num4}%) - you hold it");
		}
		return (Want: EnmityWant.Cover,Why: $"the familiar is at {familiarHp:0}% - it shares the damage with you");
	}
	public static void Note(string why) {
		LastWhy=why;
	}
}
