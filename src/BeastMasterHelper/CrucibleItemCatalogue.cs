using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lumina.Excel;
namespace Beastmaster;

public static class CrucibleItemCatalogue {
	private const string SheetName="XBMItem";
	private static readonly Regex DurationPattern=new Regex("Duration:\\s*(\\d+)s",RegexOptions.Compiled|RegexOptions.CultureInvariant);
	private static readonly Regex HealPattern=new Regex("[Rr]estor(?:es|ing)\\s+(\\d+)\\s*%",RegexOptions.Compiled|RegexOptions.CultureInvariant);
	private static IReadOnlyDictionary<int,CrucibleItem>? cache;
	public static IReadOnlyDictionary<int,CrucibleItem> All => cache ?? (cache=Load());
	public static CrucibleItem? Get(int id) {
		if(!All.TryGetValue(id,out CrucibleItem value)) {
			return null;
		}
		return value;
	}
	public static CrucibleItem? ByName(string name) {
		if(string.IsNullOrWhiteSpace(name)) {
			return null;
		}
		string b=name.Trim();
		foreach(CrucibleItem value in All.Values) {
			if(string.Equals(value.Name,b,StringComparison.OrdinalIgnoreCase)) {
				return value;
			}
		}
		return null;
	}
	public static void ClearCache() {
		cache=null;
	}
	private static IReadOnlyDictionary<int,CrucibleItem> Load() {
		Dictionary<int,CrucibleItem> dictionary=new Dictionary<int,CrucibleItem>();
		try {
			foreach(RawRow item in DalamudApi.DataManager.Excel.GetSheet<RawRow>(null,"XBMItem")) {
				string text=Text(item,11);
				if(!string.IsNullOrWhiteSpace(text)) {
					CrucibleItemKind kind=(CrucibleItemKind)Number(item,0);
					string text2=Text(item,12);
					IReadOnlyList<CrucibleBonus> bonuses=CrucibleItemBonuses.Parse(text2);
					dictionary[(int)item.RowId]=new CrucibleItem((int)item.RowId,kind,(uint)Number(item,1),Number(item,2),text,text2,Text(item,13)) {
						Singular = Text(item,3),
						Plural = Text(item,5),
						HealPercent = ParseHeal(text2),
						BuffSeconds = ParseDuration(text2),
						Bonuses = bonuses,
						BonusScore = CrucibleItemBonuses.Score(bonuses),
						HealsArea = text2.Contains("nearby allies",StringComparison.OrdinalIgnoreCase)
					};
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read the {Sheet} sheet.","XBMItem");
		}
		return dictionary;
	}
	private static int ParseDuration(string effect) {
		if(string.IsNullOrWhiteSpace(effect)) {
			return 0;
		}
		int num=0;
		foreach(Match item in DurationPattern.Matches(effect)) {
			if(int.TryParse(item.Groups[1].Value,out var result)&&result>num) {
				num=result;
			}
		}
		return num;
	}
	private static int ParseHeal(string effect) {
		if(string.IsNullOrWhiteSpace(effect)) {
			return 0;
		}
		int num=0;
		foreach(Match item in HealPattern.Matches(effect)) {
			if(int.TryParse(item.Groups[1].Value,out var result)&&result>num) {
				num=Math.Clamp(result,0,100);
			}
		}
		return num;
	}
	private static string Text(RawRow row,int column) {
		try {
			return row.ReadColumn(column)?.ToString()?.Trim() ?? string.Empty;
		}
		catch {
			return string.Empty;
		}
	}
	private static int Number(RawRow row,int column) {
		try {
			return Convert.ToInt32(row.ReadColumn(column));
		}
		catch {
			return 0;
		}
	}
}
