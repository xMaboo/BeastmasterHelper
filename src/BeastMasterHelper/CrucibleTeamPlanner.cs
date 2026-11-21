using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace Beastmaster;

public static class CrucibleTeamPlanner {
	private static readonly string[] DamageTypes=new string[10] { "Fire", "Ice", "Wind", "Earth", "Lightning", "Water", "Unaspected", "Slashing", "Piercing", "Blunt" };
	public const double MaxMissingHealth=0.15;
	public const int RestCapacity=3;
	public const double WorthRestingBelow=0.8;
	public static string[] DamageFromDescription(string text) {
		return DamageTypes.Where((string t) => Regex.IsMatch(text,"\\b"+t+"(?:-aspected)?\\s+(?:damage|physical attack)\\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)).ToArray();
	}
	public static string[] WeaknessTypes(string text) {
		return DamageTypes.Where((string t) => Regex.IsMatch(text,"\\b"+t+"\\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)).ToArray();
	}
	public static int Score(CrucibleFamiliar familiar,IReadOnlyCollection<string> weaknesses) {
		return familiar.Profile.PrimaryDamage.Count(weaknesses.Contains)*2+familiar.Profile.SecondaryDamage.Count(weaknesses.Contains);
	}
	private static bool HealthyEnough(CrucibleFamiliar familiar) {
		if(familiar.MaxHP!=0) {
			return (double)familiar.HP/(double)familiar.MaxHP>=0.85;
		}
		return false;
	}
	public static CrucibleFamiliar[] Suggest(CrucibleFightState state) {
		if(!state.Valid) {
			return Array.Empty<CrucibleFamiliar>();
		}
		CrucibleFamiliar[] array=Rank(state,state.Familiars.Where(HealthyEnough));
		if(array.Length<3) {
			return Rank(state,state.Familiars);
		}
		return array;
	}
	public static CrucibleFamiliar[] RestSuggestion(IEnumerable<CrucibleFamiliar> familiars) {
		return RestSuggestion(familiars,3);
	}
	public static CrucibleFamiliar[] RestSuggestion(IEnumerable<CrucibleFamiliar> familiars,int capacity) {
		List<CrucibleFamiliar> list=familiars.Where((CrucibleFamiliar f) => f.HP!=0&&f.MaxHP!=0&&f.HP<f.MaxHP).DistinctBy((CrucibleFamiliar f) => f.CatalogueId).ToList();
		List<CrucibleFamiliar> list2=list.Where((CrucibleFamiliar f) => (double)f.HP/(double)f.MaxHP<=0.8).ToList();
		return (from f in (list2.Count>0)?list2:list
			orderby (double)f.HP/(double)f.MaxHP descending,f.CatalogueId
			select f).Take(Math.Clamp(capacity,0,3)).ToArray();
	}
	public static bool SuggestionIsHurt(CrucibleFightState state) {
		if(state.Valid) {
			return Rank(state,state.Familiars.Where(HealthyEnough)).Length<3;
		}
		return false;
	}
	private static CrucibleFamiliar[] Rank(CrucibleFightState state,IEnumerable<CrucibleFamiliar> familiars) {
		return (from f in familiars
			where f.HP!=0&&f.MaxHP!=0
			orderby Score(f,state.WeaknessTypes) descending,(double)f.HP/(double)f.MaxHP descending,(f.Slot>=3)?1:0,f.CatalogueId
			select f).DistinctBy((CrucibleFamiliar f) => f.CatalogueId).Take(3).ToArray();
	}
}
