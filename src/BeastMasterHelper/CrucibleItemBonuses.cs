using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace Beastmaster;

public static class CrucibleItemBonuses {
	private static readonly Regex Suffixed=new Regex("([A-Za-z][A-Za-z' ]{2,40}?)\\s*([+-])\\s*(\\d+(?:\\.\\d+)?)\\s*%",RegexOptions.Compiled|RegexOptions.CultureInvariant);
	private static readonly Regex Phrased=new Regex("[Ii]ncreases\\s+([a-z][a-z' ]{2,40}?)\\s+by\\s+(\\d+(?:\\.\\d+)?)\\s*%",RegexOptions.Compiled|RegexOptions.CultureInvariant);
	private static readonly Regex Reduces=new Regex("[Rr]educes\\s+([a-z][a-z' ]{2,40}?)\\s+by\\s+(\\d+(?:\\.\\d+)?)\\s*%",RegexOptions.Compiled|RegexOptions.CultureInvariant);
	private static readonly (string Match,float Weight)[] Weights=new (string,float)[19] {
		("damage dealt",3f),
		("physical damage dealt",3f),
		("magic damage dealt",3f),
		("damage taken",2.5f),
		("vulnerability",2.5f),
		("maximum hp",2f),
		("critical hit rate",1.8f),
		("critical hit power",0.6f),
		("block",1f),
		("evasion",1f),
		("hp recovery",1f),
		("movement speed",0.3f),
		("resistance",0.15f),
		("haste",2f),
		("rapid recast",1.5f),
		("tp accumulation",1.2f),
		("territory tokens",0.4f),
		("loot drop rate",0.25f),
		("slow",-1.5f)
	};
	public static IReadOnlyList<CrucibleBonus> Parse(string effect) {
		List<CrucibleBonus> list=new List<CrucibleBonus>();
		if(string.IsNullOrWhiteSpace(effect)) {
			return list;
		}
		foreach(Match item in Suffixed.Matches(effect)) {
			string text=Clean(item.Groups[1].Value);
			if(text.Length!=0&&float.TryParse(item.Groups[3].Value,out var result)) {
				list.Add(new CrucibleBonus(text,(item.Groups[2].Value=="-")?(0f-result):result));
			}
		}
		foreach(Match item2 in Phrased.Matches(effect)) {
			string text2=Clean(item2.Groups[1].Value);
			if(text2.Length>0&&float.TryParse(item2.Groups[2].Value,out var result2)) {
				list.Add(new CrucibleBonus(text2,result2));
			}
		}
		foreach(Match item3 in Reduces.Matches(effect)) {
			string text3=Clean(item3.Groups[1].Value);
			if(text3.Length>0&&float.TryParse(item3.Groups[2].Value,out var result3)) {
				list.Add(new CrucibleBonus(text3,0f-result3));
			}
		}
		return list;
	}
	public static float Score(IReadOnlyList<CrucibleBonus> bonuses) {
		float num=0f;
		foreach(CrucibleBonus bonuse in bonuses) {
			float num2=WeightFor(bonuse.Stat);
			float num3=((bonuse.Stat.Contains("taken",StringComparison.OrdinalIgnoreCase)||bonuse.Stat.Contains("vulnerability",StringComparison.OrdinalIgnoreCase))?(0f-bonuse.Percent):bonuse.Percent);
			num+=num3*num2;
		}
		return num;
	}
	private static float WeightFor(string stat) {
		float result=0f;
		int num=0;
		(string,float)[] weights=Weights;
		for(int i=0;i<weights.Length;i++) {
			var (text,num2)=weights[i];
			if(stat.Contains(text,StringComparison.OrdinalIgnoreCase)&&text.Length>num) {
				result=num2;
				num=text.Length;
			}
		}
		if(num!=0) {
			return result;
		}
		return 0.3f;
	}
	private static string Clean(string raw) {
		string text=raw.Trim().Trim(new char[4] { '.', ',', ':', ';' });
		string[] array=text.Split(' ',StringSplitOptions.RemoveEmptyEntries);
		if(array.Length>4) {
			text=string.Join(' ',array[^4..]);
		}
		return text;
	}
}
