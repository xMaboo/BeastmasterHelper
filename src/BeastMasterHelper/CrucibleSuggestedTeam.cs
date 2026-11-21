using System;
using System.Collections.Generic;
using System.Linq;
namespace Beastmaster;

public static class CrucibleSuggestedTeam {
	public static CrucibleFamiliar[]? ForFight(BeastmasterConfiguration configuration,CrucibleFightState state,out string note) {
		note=string.Empty;
		CrucibleFightPlan.Active=null;
		if(!configuration.CrucibleSuggestedTeamMode||!configuration.CrucibleGuideUseFightTeams||!state.Valid) {
			return null;
		}
		CrucibleGuideFight crucibleGuideFight=CrucibleBoardGuide.FindFight(configuration.CrucibleBoard,state.Enemy);
		if(crucibleGuideFight==null) {
			note=((state.Enemy.Length==0)?"Suggested Team Mode: no enemy shown, so the usual team choice is used.":("Suggested Team Mode: "+state.Enemy+" is not in the guide data yet, so the usual team choice is used."));
			return null;
		}
		int[] wanted=((configuration.CrucibleFightTeamOverrides.TryGetValue(CrucibleBoardGuide.TeamKey(configuration.CrucibleBoard,crucibleGuideFight),out int[] value)&&value.Length==3)?value:crucibleGuideFight.Team);
		if(CrucibleFightPlan.Active?.Name!=crucibleGuideFight.Name) {
			CrucibleFightPlan.ResetCadence();
			CrucibleEnmityPlan.Reset();
		}
		CrucibleFightPlan.Active=crucibleGuideFight;
		CrucibleFightPlan.ActiveBoard=configuration.CrucibleBoard;
		CrucibleFightPlan.Team=wanted;
		List<CrucibleFamiliar> usable=state.Familiars.Where((CrucibleFamiliar f) => f.HP!=0&&f.MaxHP!=0&&(double)f.HP/(double)f.MaxHP>=0.4).ToList();
		List<CrucibleFamiliar> team=(from id in wanted
			select usable.FirstOrDefault((CrucibleFamiliar f) => f.CatalogueId==id) into f
			where f!=null
			select f).Cast<CrucibleFamiliar>().DistinctBy((CrucibleFamiliar f) => f.CatalogueId).ToList();
		int num=wanted.Length-team.Count;
		foreach(int item in wanted.Where((int w) => team.All((CrucibleFamiliar t) => t.CatalogueId!=w))) {
			BeastKin kin=BeastmasterKinship.KinOf(item);
			CrucibleFamiliar crucibleFamiliar=((kin==BeastKin.Unknown)?null:(from f in usable
				where BeastmasterKinship.KinOf(f.CatalogueId)==kin&&team.All((CrucibleFamiliar t) => t.CatalogueId!=f.CatalogueId)&&!wanted.Contains(f.CatalogueId)
				orderby f.CatalogueId
				select f).FirstOrDefault());
			if(crucibleFamiliar!=null) {
				team.Add(crucibleFamiliar);
			}
		}
		if(team.Count<3) {
			CrucibleFightState state2=state with {
				Familiars = state.Familiars.Where((CrucibleFamiliar f) => team.All((CrucibleFamiliar t) => t.CatalogueId!=f.CatalogueId)).ToList()
			};
			team.AddRange(CrucibleTeamPlanner.Suggest(state2).Take(3-team.Count));
		}
		CrucibleFightPlan.Equipped=team.Select((CrucibleFamiliar f) => f.CatalogueId).ToArray();
		note=((num==0)?("Suggested Team Mode: the guide's team for "+crucibleGuideFight.Name+"."):$"Suggested Team Mode: the guide's team for {crucibleGuideFight.Name}, with {num} replaced - not available or too hurt.");
		return team.ToArray();
	}
	public static CrucibleFamiliar[] MatchGuideKinships(BeastmasterConfiguration configuration,CrucibleFightState state,CrucibleFamiliar[] suggestion,out string note) {
		note=string.Empty;
		CrucibleGuideFight crucibleGuideFight=(state.Valid?CrucibleBoardGuide.FindFight(configuration.CrucibleBoard,state.Enemy):null);
		if(crucibleGuideFight==null) {
			return suggestion;
		}
		List<BeastKin> list=(from k in ((configuration.CrucibleFightTeamOverrides.TryGetValue(CrucibleBoardGuide.TeamKey(configuration.CrucibleBoard,crucibleGuideFight),out int[] value)&&value.Length==3)?value:crucibleGuideFight.Team).Select(BeastmasterKinship.KinOf)
			where k!=BeastKin.Unknown
			select k).ToList();
		if(list.Count==0) {
			return suggestion;
		}
		List<CrucibleFamiliar> source=state.Familiars.Where((CrucibleFamiliar f) => f.HP!=0&&f.MaxHP!=0&&(double)f.HP/(double)f.MaxHP>=0.4).ToList();
		List<CrucibleFamiliar> team=suggestion.ToList();
		List<string> list2=new List<string>();
		List<BeastKin> list3=new List<BeastKin>(list);
		foreach(CrucibleFamiliar item2 in team.ToList()) {
			BeastKin item=BeastmasterKinship.KinOf(item2.CatalogueId);
			list3.Remove(item);
		}
		foreach(BeastKin kin in list3.ToList()) {
			CrucibleFamiliar crucibleFamiliar=(from f in source
				where team.All((CrucibleFamiliar t) => t.CatalogueId!=f.CatalogueId)&&BeastmasterKinship.KinOf(f.CatalogueId)==kin
				orderby CrucibleTeamPlanner.Score(f,state.WeaknessTypes) descending,(f.MaxHP==0)?0.0:((double)f.HP/(double)f.MaxHP) descending,f.CatalogueId
				select f).FirstOrDefault();
			if(crucibleFamiliar==null) {
				continue;
			}
			if(team.Count<3) {
				team.Add(crucibleFamiliar);
				list2.Add(crucibleFamiliar.Name);
				continue;
			}
			List<BeastKin> list4=new List<BeastKin>(list);
			List<CrucibleFamiliar> list5=new List<CrucibleFamiliar>();
			foreach(CrucibleFamiliar item3 in team) {
				if(!list4.Remove(BeastmasterKinship.KinOf(item3.CatalogueId))) {
					list5.Add(item3);
				}
			}
			CrucibleFamiliar crucibleFamiliar2=list5.OrderBy((CrucibleFamiliar t) => CrucibleTeamPlanner.Score(t,state.WeaknessTypes)).FirstOrDefault();
			if(!(crucibleFamiliar2==null)) {
				team[team.IndexOf(crucibleFamiliar2)]=crucibleFamiliar;
				list2.Add(crucibleFamiliar.Name+" for "+crucibleFamiliar2.Name);
			}
		}
		if(list2.Count>0) {
			note=$"Matching the guide's kinships for {crucibleGuideFight.Name}: {string.Join(", ",list2)}.";
		}
		return team.ToArray();
	}
	public static IReadOnlyList<int> EntryPool(BeastmasterConfiguration configuration) {
		if(!configuration.CrucibleSuggestedTeamMode||!configuration.CrucibleGuideUseEntryPool) {
			return Array.Empty<int>();
		}
		CrucibleGuideBoard crucibleGuideBoard=CrucibleBoardGuide.ForBoard(configuration.CrucibleBoard);
		if(crucibleGuideBoard==null) {
			return Array.Empty<int>();
		}
		string prefix=$"{configuration.CrucibleBoard}:";
		IEnumerable<int> first=configuration.CrucibleFightTeamOverrides.Where((KeyValuePair<string,int[]> kv) => kv.Key.StartsWith(prefix,StringComparison.Ordinal)&&kv.Value.Length==3).SelectMany((KeyValuePair<string,int[]> kv) => kv.Value);
		List<int> list=new List<int>();
		list.AddRange(first.Concat(crucibleGuideBoard.EntryPool).Distinct());
		return list;
	}
}
