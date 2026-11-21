using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Lumina.Excel.Sheets;
namespace Beastmaster;

public static class CrucibleFightPlan {
	private const float BusterLead=4.5f;
	private const float RaidwideLead=3f;
	private static readonly HashSet<string> SeenCasts=new HashSet<string>();
	private static readonly Dictionary<uint,DamageKind> KindCache=new Dictionary<uint,DamageKind>();
	private static readonly Dictionary<string,DamageKind> DominantCache=new Dictionary<string,DamageKind>();
	private static DamageKind lastKind=DamageKind.Unknown;
	private static string lastFight=string.Empty;
	private static readonly Dictionary<BeastKin,DateTime> UsedAt=new Dictionary<BeastKin,DateTime>();
	private static readonly Dictionary<uint,string> StatusNames=new Dictionary<uint,string>();
	private static DateTime lastBusterAt=DateTime.MinValue;
	private static double busterInterval;
	private static bool busterWasActive;
	private static bool openerBorrowSeen;
	private static bool openerGivenUp;
	private static BeastKin lentKin=BeastKin.Unknown;
	public static CrucibleGuideFight? Active { get; set; }
	public static int ActiveBoard { get; set; }
	public static BossModBridge? Bridge { get; set; }
	public static int[] Team { get; set; }=Array.Empty<int>();
	public static int[] Equipped { get; set; }=Array.Empty<int>();
	public static DateTime LastPlanSwap { get; set; }=DateTime.MinValue;
	public static bool HandedToReactionHelper(BeastmasterConfiguration configuration,string encounter) {
		if(encounter.Length==0) {
			return false;
		}
		foreach(string crucibleReactionHelperFight in configuration.CrucibleReactionHelperFights) {
			int num=crucibleReactionHelperFight.IndexOf(':');
			string text=((num>=0)?crucibleReactionHelperFight.Substring(num+1):crucibleReactionHelperFight);
			if(text.Length>0&&(encounter.Contains(text,StringComparison.OrdinalIgnoreCase)||text.Contains(encounter,StringComparison.OrdinalIgnoreCase))) {
				return true;
			}
		}
		return false;
	}
	public static bool Applies(BeastmasterConfiguration configuration) {
		if(!configuration.CrucibleSuggestedTeamMode||Active==null) {
			return false;
		}
		if(configuration.CrucibleReactionHelperFights.Contains(CrucibleBoardGuide.TeamKey(ActiveBoard,Active))) {
			return false;
		}
		if(!DalamudApi.Condition[ConditionFlag.BoundByDuty]) {
			Active=null;
			return false;
		}
		return true;
	}
	public static string UseKey(int board,CrucibleGuideFight fight,int number) {
		return $"{board}:{fight.Name}:{number}";
	}
	public static (PlannedUse Use,string Why,bool Custom) UseFor(BeastmasterConfiguration configuration,int board,CrucibleGuideFight fight,int number) {
		OneWithNatureChoice? oneWithNatureChoice=RotationToggleOverrides.NatureChoice(configuration);
		if(oneWithNatureChoice.HasValue) {
			OneWithNatureChoice valueOrDefault=oneWithNatureChoice.GetValueOrDefault();
			return (Use: (valueOrDefault==OneWithNatureChoice.Borrow)?PlannedUse.Borrow:PlannedUse.TemperedRelease,Why: "your Borrow / Tempered Release switch",Custom: false);
		}
		var (item,item2)=RecommendedUse(fight,number);
		return (Use: item,Why: item2,Custom: false);
	}
	private static int KinBorrower(CrucibleGuideFight fight,BeastKin kin) {
		List<int> source=fight.Team.Where((int n) => BeastmasterKinship.KinOf(n)==kin).ToList();
		return source.FirstOrDefault((int n) => FamiliarAbilityData.Get(n)?.TemperedLeaves ?? false,source.FirstOrDefault());
	}
	public static (PlannedUse Use,string Why) RecommendedUse(CrucibleGuideFight fight,int number) {
		BeastKin beastKin=BeastmasterKinship.KinOf(number);
		if(fight.BorrowKins.Contains(beastKin,null)) {
			return (Use: PlannedUse.Borrow,Why: BeastmasterKinship.Effect(beastKin)+" - what the strategy needs from it");
		}
		FamiliarInfo familiarInfo=FamiliarAbilityData.Get(number);
		if((object)familiarInfo!=null&&familiarInfo.TemperedLeaves) {
			return (Use: PlannedUse.Borrow,Why: "its Tempered Release makes it leave the fight, so it is Borrowed to keep it here");
		}
		if(fight.BorrowKins.Length!=0) {
			return (Use: PlannedUse.TemperedRelease,Why: "damage from its Tempered Release - this fight's Borrow is spoken for");
		}
		if(BeastmasterKinship.IsMitigation(beastKin)) {
			int num=KinBorrower(fight,beastKin);
			if(num!=0&&num!=number) {
				string text=FamiliarAbilityData.Get(num)?.Name ?? "another familiar";
				return (Use: PlannedUse.TemperedRelease,Why: "damage from its Tempered Release - "+text+" already lends "+BeastmasterKinship.Effect(beastKin));
			}
		}
		if(BeastmasterKinship.IsMitigation(beastKin)) {
			string text2=beastKin switch {
				BeastKin.Beastkin => "physical damage",
				BeastKin.Scalekin => "magic damage",
				_ => "a tank buster",
			};
			string text3=((fight.Busters.Length!=0)?("; also ahead of "+string.Join(" / ",fight.Busters)):string.Empty);
			return (Use: PlannedUse.Borrow,Why: BeastmasterKinship.Effect(beastKin)+" - mitigates "+text2+text3);
		}
		return (Use: PlannedUse.TemperedRelease,Why: "damage from its Tempered Release");
	}
	public static PlannedUse? NatureChoice(BeastmasterConfiguration configuration,int number) {
		if(!Applies(configuration)) {
			return null;
		}
		return UseFor(configuration,ActiveBoard,Active,number).Use;
	}
	public static DamageKind KindOfKin(BeastKin kin) {
		return kin switch {
			BeastKin.Beastkin => DamageKind.Physical,
			BeastKin.Scalekin => DamageKind.Magic,
			_ => DamageKind.Unknown,
		};
	}
	public static DamageKind KindOfAction(uint actionId) {
		if(actionId==0) {
			return DamageKind.Unknown;
		}
		if(KindCache.TryGetValue(actionId,out var value)) {
			return value;
		}
		DamageKind damageKind=DamageKind.Unknown;
		try {
			if(DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId,out var row)) {
				DamageKind damageKind2;
				switch(row.AttackType.RowId) {
				case 1u:
				case 2u:
				case 3u:
				case 4u:
				case 7u:
				damageKind2=DamageKind.Physical;
				break;
				case 5u:
				case 6u:
				damageKind2=DamageKind.Magic;
				break;
				default:
				damageKind2=DamageKind.Unknown;
				break;
				}
				damageKind=damageKind2;
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read the attack type of action {Action}.",actionId);
		}
		KindCache[actionId]=damageKind;
		return damageKind;
	}
	public static DamageKind DominantKind(CrucibleGuideFight fight) {
		if(DominantCache.TryGetValue(fight.Name,out var value)) {
			return value;
		}
		HashSet<string> hashSet=(from a in fight.Abilities
			select (!a.Contains(" - ",StringComparison.Ordinal))?a:a.Substring(a.IndexOf(" - ",StringComparison.Ordinal)+3) into a
			select a.Split(':')[0].Trim() into a
			where a.Length>0&&!a.Contains('(')&&!a.Contains('?')
			select a).ToHashSet(StringComparer.OrdinalIgnoreCase);
		int num=0;
		int num2=0;
		try {
			foreach(Lumina.Excel.Sheets.Action item in DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()) {
				string text=item.Name.ExtractText();
				if(text.Length!=0&&hashSet.Contains(text)) {
					switch(KindOfAction(item.RowId)) {
					case DamageKind.Physical:
					num++;
					break;
					case DamageKind.Magic:
					num2++;
					break;
					}
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not work out the damage kind of {Fight}.",fight.Name);
		}
		DamageKind damageKind=((num2>num)?DamageKind.Magic:((num>num2)?DamageKind.Physical:DamageKind.Unknown));
		DalamudApi.Log.Information("Fight plan: {Fight} looks {Kind} ({Physical} physical, {Magic} magic abilities found).",fight.Name,damageKind,num,num2);
		DominantCache[fight.Name]=damageKind;
		return damageKind;
	}
	public static DamageKind Expected(CrucibleGuideFight fight) {
		if(!(lastFight==fight.Name)||lastKind==DamageKind.Unknown) {
			return DominantKind(fight);
		}
		return lastKind;
	}
	public static bool NeedsBorrowNow(BeastmasterConfiguration configuration,int number,BeastKin borrowed) {
		if(!Applies(configuration)) {
			return false;
		}
		BeastKin beastKin=BeastmasterKinship.KinOf(number);
		if(beastKin!=BeastKin.Unknown&&beastKin!=borrowed) {
			return UseFor(configuration,ActiveBoard,Active,number).Use==PlannedUse.Borrow;
		}
		return false;
	}
	public static bool CoversExpected(BeastmasterConfiguration configuration,int number) {
		DamageKind damageKind=KindOfKin(BeastmasterKinship.KinOf(number));
		if(damageKind==DamageKind.Unknown||!Applies(configuration)) {
			return true;
		}
		DamageKind damageKind2=Expected(Active);
		if(damageKind2!=DamageKind.Unknown) {
			return damageKind2==damageKind;
		}
		return true;
	}
	public static EnemyCast? Observe(BeastmasterConfiguration configuration) {
		if(!Applies(configuration)) {
			return null;
		}
		EnemyCast enemyCast=null;
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(!(item is IBattleNpc battleNpc)||item.ObjectKind!=ObjectKind.BattleNpc||battleNpc.IsDead||!battleNpc.IsCasting) {
				continue;
			}
			string name=BeastmasterGameData.ActionName(battleNpc.CastActionId,string.Empty);
			if(name.Length!=0) {
				DamageKind damageKind=KindOfAction(battleNpc.CastActionId);
				bool flag=Active.Busters.Any((string b) => name.Contains(b,StringComparison.OrdinalIgnoreCase));
				if(SeenCasts.Add(name)) {
					DalamudApi.Log.Information("Fight plan: {Enemy} is casting '{Cast}' ({Kind}{Buster}).",battleNpc.Name.TextValue,name,damageKind,flag?", tank buster":string.Empty);
				}
				if(damageKind!=DamageKind.Unknown) {
					lastKind=damageKind;
					lastFight=Active.Name;
				}
				if((enemyCast==null)|flag) {
					enemyCast=new EnemyCast(name,damageKind,flag,IsRaidwide: false,Math.Max(0f,battleNpc.TotalCastTime-battleNpc.CurrentCastTime));
				}
			}
		}
		NoteBuster(enemyCast?.IsBuster ?? false);
		if((object)enemyCast==null||!enemyCast.IsBuster) {
			BossModBridge bridge=Bridge;
			if(bridge!=null) {
				float nextTankbusterIn=bridge.NextTankbusterIn;
				if(nextTankbusterIn>-0.5f&&nextTankbusterIn<=4.5f) {
					enemyCast=new EnemyCast($"a tank buster in {Math.Max(0f,nextTankbusterIn):0.0}s (BossMod)",DamageKind.Unknown,IsBuster: true,IsRaidwide: false,Math.Max(0f,nextTankbusterIn));
				}
				else if(enemyCast==null) {
					float nextRaidwideIn=bridge.NextRaidwideIn;
					if(nextRaidwideIn>-0.5f&&nextRaidwideIn<=3f) {
						enemyCast=new EnemyCast($"a raidwide in {Math.Max(0f,bridge.NextRaidwideIn):0.0}s (BossMod)",DamageKind.Unknown,IsBuster: false,IsRaidwide: true);
					}
				}
			}
		}
		return enemyCast;
	}
	public static void NoteBeastMode(BeastKin kin) {
		UsedAt[kin]=DateTime.UtcNow;
	}
	private static bool RecentlyUsed(BeastKin kin) {
		if(UsedAt.TryGetValue(kin,out var value)) {
			return (DateTime.UtcNow-value).TotalSeconds<20.0;
		}
		return false;
	}
	private static IEnumerable<IBattleNpc> LiveEnemies() {
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(item is IBattleNpc battleNpc&&item.ObjectKind==ObjectKind.BattleNpc&&!battleNpc.IsDead&&battleNpc.CurrentHp!=0&&battleNpc.IsTargetable) {
				yield return battleNpc;
			}
		}
	}
	public static bool InterruptWorthIt(BeastmasterConfiguration configuration,IBattleChara? target,out string name) {
		name=string.Empty;
		if(!Applies(configuration)) {
			return false;
		}
		if((Bridge?.InterruptCastRemaining ?? float.MaxValue)<1000f) {
			name=((target!=null&&target.IsCasting)?BeastmasterGameData.ActionName(target.CastActionId,"a cast"):"the cast BossMod flagged");
			return true;
		}
		if(target==null||!target.IsCasting||!target.IsCastInterruptible) {
			return false;
		}
		string cast=BeastmasterGameData.ActionName(target.CastActionId,"a cast");
		name=cast;
		CrucibleGuideFight active=Active;
		if(active.Interrupts.Length!=0) {
			return active.Interrupts.Any((string i) => cast.Contains(i,StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}
	public static bool InterruptPending(BeastmasterConfiguration configuration) {
		if(!Applies(configuration)) {
			return false;
		}
		if((Bridge?.InterruptCastRemaining ?? float.MaxValue)<1000f) {
			return true;
		}
		CrucibleGuideFight active=Active;
		foreach(IBattleNpc item in LiveEnemies()) {
			if(item.IsCasting&&item.IsCastInterruptible) {
				string name=BeastmasterGameData.ActionName(item.CastActionId,string.Empty);
				if(active.Interrupts.Length==0||active.Interrupts.Any((string i) => name.Contains(i,StringComparison.OrdinalIgnoreCase))) {
					return true;
				}
			}
		}
		return false;
	}
	public static bool DispelPending(BeastmasterConfiguration configuration) {
		if(!Applies(configuration)) {
			return false;
		}
		BossModBridge? bridge=Bridge;
		if(bridge!=null&&bridge.ShouldDispel) {
			return true;
		}
		string[] dispels=Active.Dispels;
		if(dispels.Length==0) {
			return false;
		}
		foreach(IBattleNpc item in LiveEnemies()) {
			foreach(IStatus status in item.StatusList) {
				if(status.StatusId==0) {
					continue;
				}
				if(!StatusNames.TryGetValue(status.StatusId,out string name)) {
					name=string.Empty;
					try {
						if(DalamudApi.DataManager.GetExcelSheet<Status>().TryGetRow(status.StatusId,out var row)) {
							name=row.Name.ExtractText();
						}
					}
					catch(Exception exception) {
						DalamudApi.Log.Warning(exception,"Could not read the name of status {Status}.",status.StatusId);
					}
					StatusNames[status.StatusId]=name;
				}
				if(name.Length>0&&dispels.Any((string w) => name.Contains(w,StringComparison.OrdinalIgnoreCase))) {
					return true;
				}
			}
		}
		return false;
	}
	private static bool BuffOrHitSoon(BeastmasterConfiguration configuration,out bool buster) {
		buster=false;
		BossModBridge bridge=Bridge;
		if(bridge==null) {
			buster=BusterDueByRhythm();
			return buster;
		}
		float num=Math.Min(bridge.NextTankbusterIn,bridge.TimelineTankbusterIn);
		buster=(num>-0.5f&&num<=30f)||BusterDueByRhythm();
		if(!buster) {
			float num2=Math.Min(bridge.NextRaidwideIn,bridge.TimelineRaidwideIn);
			if(num2>-0.5f) {
				return num2<=20f;
			}
			return false;
		}
		return true;
	}
	public static int KinNeed(BeastmasterConfiguration configuration,BeastKin kin,int number) {
		int num=KinNeed(configuration,kin);
		if(number<=0||!FamiliarUtility.IsTank(number)) {
			return num;
		}
		return Math.Max(num,45);
	}
	public static int KinNeed(BeastmasterConfiguration configuration,BeastKin kin) {
		if(!Applies(configuration)||kin==BeastKin.Unknown) {
			return 0;
		}
		CrucibleGuideFight active=Active;
		int num;
		switch(kin) {
		case BeastKin.Soulkin:
		if(!active.BorrowKins.Contains(kin,null)) {
			return 0;
		}
		num=(InterruptPending(configuration)?92:50);
		break;
		case BeastKin.Ashkin:
		if(!active.BorrowKins.Contains(kin,null)) {
			return 0;
		}
		num=(BeastmasterRotationService.HasRemovableDebuff()?96:40);
		break;
		case BeastKin.Wavekin:
		if(!active.BorrowKins.Contains(kin,null)) {
			return 0;
		}
		num=(DispelPending(configuration)?90:35);
		break;
		case BeastKin.Beastkin:
		case BeastKin.Scalekin: {
			bool flag=BuffOrHitSoon(configuration,out var buster2);
			DamageKind damageKind=KindOfKin(kin);
			DamageKind damageKind2=Expected(active);
			bool flag2=damageKind==DamageKind.Unknown||damageKind2==DamageKind.Unknown||damageKind2==damageKind;
			if(buster2) {
				num=(flag2?92:80);
			}
			else if(flag&&kin==BeastKin.Scalekin) {
				num=60;
			}
			else if(active.Busters.Length!=0) {
				num=(flag2?55:25);
			}
			else {
				num=(flag2?30:10);
			}
			break;
		}
		case BeastKin.Vilekin: {
			if(BuffOrHitSoon(configuration,out var buster)&buster) {
				num=80;
			}
			else {
				num=((active.Busters.Length!=0)?20:5);
			}
			break;
		}
		default:
		return 0;
		}
		if(!RecentlyUsed(kin)) {
			return num;
		}
		return Math.Min(num,10);
	}
	public static BeastKin PlanLent(BeastKin live) {
		return live;
	}
	public static void ResetCadence() {
		lentKin=BeastKin.Unknown;
		lastBusterAt=DateTime.MinValue;
		busterInterval=0.0;
		busterWasActive=false;
		openerBorrowSeen=false;
		openerGivenUp=false;
	}
	public static bool WantsOpener(out BeastKin kin) {
		kin=((!openerBorrowSeen&&!openerGivenUp)?(Active?.OpenWith ?? BeastKin.Unknown):BeastKin.Unknown);
		return kin!=BeastKin.Unknown;
	}
	public static void NoteBorrowed(BeastKin borrowed) {
		if(borrowed!=BeastKin.Unknown) {
			lentKin=borrowed;
		}
		BeastKin? beastKin=Active?.OpenWith;
		if(beastKin.HasValue) {
			BeastKin valueOrDefault=beastKin.GetValueOrDefault();
			if(valueOrDefault!=BeastKin.Unknown&&borrowed==valueOrDefault) {
				openerBorrowSeen=true;
			}
		}
	}
	public static void GiveUpOnOpener() {
		openerGivenUp=true;
	}
	private static void NoteBuster(bool active) {
		if(active&&!busterWasActive) {
			DateTime utcNow=DateTime.UtcNow;
			if(lastBusterAt!=DateTime.MinValue) {
				double totalSeconds=(utcNow-lastBusterAt).TotalSeconds;
				if(totalSeconds>15.0&&totalSeconds<240.0) {
					busterInterval=totalSeconds;
					DalamudApi.Log.Information("Fight plan: a tank buster is coming round about every {Gap:0}s.",totalSeconds);
				}
			}
			lastBusterAt=utcNow;
		}
		busterWasActive=active;
	}
	public static bool BusterWithin(double seconds) {
		if(busterInterval<=0.0||lastBusterAt==DateTime.MinValue) {
			return false;
		}
		double totalSeconds=(DateTime.UtcNow-lastBusterAt).TotalSeconds;
		if(totalSeconds>=busterInterval-seconds) {
			return totalSeconds<=busterInterval+5.0;
		}
		return false;
	}
	public static bool MitigationReady() {
		if(!BeastmasterKinship.IsMitigation(BeastmasterKinship.Borrowed())&&!RecentlyUsed(BeastKin.Beastkin)&&!RecentlyUsed(BeastKin.Scalekin)) {
			return RecentlyUsed(BeastKin.Vilekin);
		}
		return true;
	}
	private static bool BusterDueByRhythm() {
		if(busterInterval<=0.0||lastBusterAt==DateTime.MinValue) {
			return false;
		}
		double totalSeconds=(DateTime.UtcNow-lastBusterAt).TotalSeconds;
		if(totalSeconds>=busterInterval-25.0) {
			return totalSeconds<=busterInterval+15.0;
		}
		return false;
	}
}
