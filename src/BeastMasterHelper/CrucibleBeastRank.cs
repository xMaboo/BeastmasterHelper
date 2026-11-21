using System.Collections.Generic;
namespace Beastmaster;

public static class CrucibleBeastRank {
	public static int BoardRankCap(int board) {
		return board switch {
			0 => 5,
			1 => 10,
			2 => 15,
			3 => 20,
			_ => 25,
		};
	}
	public static List<(int CatalogueId,int Rank)> LevelingCandidatesForCap(IEnumerable<int> catalogueIds,IReadOnlyDictionary<uint,int> learned,IReadOnlyDictionary<int,int> fallback,IReadOnlyDictionary<uint,int> expPercent,int cap) {
		List<(int,int)> list=new List<(int,int)>();
		foreach(int catalogueId in catalogueIds) {
			int? num=EffectiveRank(learned,fallback,catalogueId);
			if(num.HasValue&&(num<cap||(num==cap&&(!expPercent.TryGetValue(MonsterIdFor(catalogueId),out var value)||value<100)))) {
				list.Add((catalogueId,num.Value));
			}
		}
		list.Sort(((int,int) a,(int,int) b) => (a.Item2==b.Item2)?a.Item1.CompareTo(b.Item1):a.Item2.CompareTo(b.Item2));
		return list;
	}
	public static int BoardTeamSize(int board) {
		return board switch {
			0 => 10,
			1 => 12,
			2 => 14,
			3 => 12,
			_ => 15,
		};
	}
	public static uint MonsterIdFor(int catalogueId) {
		return (uint)(242000+catalogueId);
	}
	public static int? EffectiveRank(IReadOnlyDictionary<uint,int> learned,IReadOnlyDictionary<int,int> fallback,int catalogueId) {
		if(learned.TryGetValue(MonsterIdFor(catalogueId),out var value)) {
			return value;
		}
		if(!fallback.TryGetValue(catalogueId,out var value2)) {
			return null;
		}
		return value2;
	}
	public static void Learn(IDictionary<uint,int> learned,uint monsterId,int rank) {
		learned[monsterId]=rank;
	}
	public static List<(int CatalogueId,int Rank)> LevelingCandidates(IEnumerable<int> catalogueIds,IReadOnlyDictionary<uint,int> learned,IReadOnlyDictionary<int,int> fallback,int threshold) {
		List<(int,int)> list=new List<(int,int)>();
		foreach(int catalogueId in catalogueIds) {
			int? num=EffectiveRank(learned,fallback,catalogueId);
			if(num.HasValue&&num<=threshold) {
				list.Add((catalogueId,num.Value));
			}
		}
		list.Sort(((int,int) a,(int,int) b) => (a.Item2==b.Item2)?a.Item1.CompareTo(b.Item1):a.Item2.CompareTo(b.Item2));
		return list;
	}
	public static List<(uint MonsterId,bool Select)> LevelingSelectionPlan(IEnumerable<int> catalogueIds,IReadOnlyDictionary<uint,int> learned,IReadOnlyDictionary<int,int> fallback,IReadOnlyDictionary<uint,bool> trackedSelected,int threshold) {
		List<(uint,bool)> list=new List<(uint,bool)>();
		foreach(int catalogueId in catalogueIds) {
			int? num=EffectiveRank(learned,fallback,catalogueId);
			if(num.HasValue) {
				uint num2=MonsterIdFor(catalogueId);
				bool flag=num<=threshold;
				if(!trackedSelected.TryGetValue(num2,out var value)||value!=flag) {
					list.Add((num2,flag));
				}
			}
		}
		return list;
	}
}
