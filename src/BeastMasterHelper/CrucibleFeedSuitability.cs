using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Lumina.Excel;
namespace Beastmaster;

public static class CrucibleFeedSuitability {
	private const string PetSheet="XBMPet";
	private static readonly Regex SuitablePattern=new Regex("Suitable for ([^\\]\\.]+)",RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.CultureInvariant);
	private static IReadOnlyDictionary<int,BeastKin>? kinCache;
	public static IReadOnlyDictionary<int,BeastKin> KinByFamiliar => kinCache ?? (kinCache=LoadKin());
	public static BeastKin KinOf(int familiarId) {
		if(!KinByFamiliar.TryGetValue(familiarId,out var value)) {
			return BeastKin.Unknown;
		}
		return value;
	}
	private static IReadOnlyDictionary<int,BeastKin> LoadKin() {
		Dictionary<int,BeastKin> dictionary=new Dictionary<int,BeastKin>();
		try {
			foreach(RawRow item in DalamudApi.DataManager.Excel.GetSheet<RawRow>(null,"XBMPet")) {
				if(item.RowId!=0) {
					int num=Convert.ToInt32(item.ReadColumn(1));
					if(num>=1&&num<=8) {
						dictionary[(int)item.RowId]=(BeastKin)num;
					}
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read the {Sheet} sheet for kinships.","XBMPet");
		}
		return dictionary;
	}
	public static IReadOnlySet<BeastKin> SuitableFor(CrucibleItem? item) {
		HashSet<BeastKin> hashSet=new HashSet<BeastKin>();
		if(item==null||string.IsNullOrWhiteSpace(item.Effect)) {
			return hashSet;
		}
		Match match=SuitablePattern.Match(item.Effect);
		if(!match.Success) {
			return hashSet;
		}
		string[] array=match.Groups[1].Value.Split(new char[2] { ',', ' ' },StringSplitOptions.RemoveEmptyEntries);
		for(int i=0;i<array.Length;i++) {
			if(Enum.TryParse<BeastKin>(array[i].Trim().TrimEnd(new char[2] { '.', ']' }),ignoreCase: true,out var result)&&result!=BeastKin.Unknown) {
				hashSet.Add(result);
			}
		}
		return hashSet;
	}
	public static bool CanReceive(CrucibleItem? item,CrucibleFamiliar familiar) {
		IReadOnlySet<BeastKin> readOnlySet=SuitableFor(item);
		if(readOnlySet.Count==0) {
			return true;
		}
		BeastKin beastKin=KinOf(familiar.CatalogueId);
		if(beastKin!=BeastKin.Unknown) {
			return readOnlySet.Contains(beastKin);
		}
		return true;
	}
	public static string Describe(CrucibleItem? item) {
		IReadOnlySet<BeastKin> readOnlySet=SuitableFor(item);
		if(readOnlySet.Count!=0) {
			return "suitable for "+string.Join(", ",readOnlySet.Select((BeastKin k) => k.ToString().ToLowerInvariant()));
		}
		return "no kinship restriction";
	}
	public static void ClearCache() {
		kinCache=null;
	}
}
