using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace Beastmaster;

public static class FamiliarUtility {
	private static readonly Dictionary<int,string[]> Cache=new Dictionary<int,string[]>();
	private static readonly Dictionary<int,string[]> FromGuide=new Dictionary<int,string[]> {
		[50]=new string[2] { "Tank", "Big AoE (Meteor)" },
		[28]=new string[2] { "Tank", "Heavy (Bottomless Desert)" },
		[35]=new string[1] { "Mitigation (Smoldering Scales)" },
		[23]=new string[1] { "Tank" },
		[31]=new string[1] { "Tank" },
		[25]=new string[1] { "Tank" }
	};
	private static readonly (Regex Pattern,string Tag)[] EffectTags=new (Regex,string)[28] {
		(R("hastens"),"Haste"),
		(R("put(?:s)? enemies to sleep"),"Sleep"),
		(R("\\bstuns?\\b"),"Stun"),
		(R("paralyz"),"Paralyze"),
		(R("\\bbinds?\\b"),"Bind"),
		(R("heaviness"),"Heavy"),
		(R("\\bslows?\\b"),"Slow"),
		(R("knocks? back"),"Knockback"),
		(R("draws in"),"Pull"),
		(R("petrif"),"Petrify"),
		(R("\\bdoom"),"Doom"),
		(R("\\bblinds?\\b"),"Blind"),
		(R("poison"),"Poison"),
		(R("sicken"),"Sicken"),
		(R("nightmare"),"Nightmare"),
		(R("freez"),"Freeze"),
		(R("reduces enemy accuracy|reduces accuracy"),"Accuracy down"),
		(R("dispels one beneficial status"),"Dispel"),
		(R("removes a status ailment from allies"),"Cleanse"),
		(R("generates beastmaster hp"),"Heal (drains HP to you)"),
		(R("hp absorption to allies"),"Life-steal buff"),
		(R("damage dealt by allies"),"Damage buff"),
		(R("keen edge"),"Edge buff"),
		(R("reduces (?:physical|magic) vulnerability[^.]*allies"),"Mitigation"),
		(R("improves [a-z, ]*resistance[^.]*allies"),"Resist buff"),
		(R("increases (?:physical|magic) vulnerability[^.]*enem"),"Vulnerability debuff"),
		(R("lowers [a-z ]*resistance[^.]*enem"),"Resistance debuff"),
		(R("retreats"),"Leaves the fight")
	};
	private static string KinshipTag(BeastKin kin) {
		return kin switch {
			BeastKin.Beastkin => "Mitigation (Beastskin)",
			BeastKin.Vilekin => "Block (Vileskin)",
			BeastKin.Cloudkin => "Dash (Cloud Skim)",
			BeastKin.Seedkin => "AoE debuff (Seedsower)",
			BeastKin.Wavekin => "Ranged, Dispel (Quelling Wave)",
			BeastKin.Scalekin => "Barrier (Scaleskin)",
			BeastKin.Soulkin => "Interrupt (Soul Crush)",
			BeastKin.Ashkin => "Cleanse (Scouring Ash)",
			_ => string.Empty,
		};
	}
	private static Regex R(string pattern) {
		return new Regex(pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
	}
	public static IReadOnlyList<string> Tags(int number) {
		if(Cache.TryGetValue(number,out string[] value)) {
			return value;
		}
		List<string> tags=new List<string>();
		BeastKin beastKin=BeastmasterKinship.KinOf(number);
		Add(KinshipTag(beastKin));
		if(FromGuide.TryGetValue(number,out string[] value2)) {
			string[] array=value2;
			for(int i=0;i<array.Length;i++) {
				Add(array[i]);
			}
		}
		FamiliarInfo familiarInfo=FamiliarAbilityData.Get(number);
		if((object)familiarInfo!=null) {
			FamiliarAbility[] array2=new FamiliarAbility[2] { familiarInfo.Tempered, familiarInfo.Trick };
			foreach(FamiliarAbility familiarAbility in array2) {
				(Regex,string)[] effectTags=EffectTags;
				for(int j=0;j<effectTags.Length;j++) {
					var (regex,tag)=effectTags[j];
					if(regex.IsMatch(familiarAbility.Effect)) {
						Add(tag);
					}
				}
			}
		}
		string[] array3=((tags.Count!=0)?tags.ToArray():new string[1] { "Damage" });
		if(beastKin!=BeastKin.Unknown) {
			Cache[number]=array3;
		}
		return array3;
		void Add(string text) {
			string key=text.Split('(')[0].Trim();
			if(text.Length>0&&!tags.Any((string t) => t.Split('(')[0].Trim().Equals(key,StringComparison.OrdinalIgnoreCase))) {
				tags.Add(text);
			}
		}
	}
	public static IReadOnlyList<string> Details(int number) {
		FamiliarInfo familiarInfo=FamiliarAbilityData.Get(number);
		if((object)familiarInfo==null) {
			return Array.Empty<string>();
		}
		return new string[4] {
			"Auto-attack: "+familiarInfo.AutoAttack,
			Line("Tempered Release",familiarInfo.Tempered)+(familiarInfo.TemperedLeaves?"  [LEAVES THE FIGHT]":string.Empty),
			Line("Trick",familiarInfo.Trick),
			"Borrow: "+familiarInfo.Borrow
		};
		static string Line(string label,FamiliarAbility ability) {
			return $"{label}: {ability.Name} - {ability.Effect}"+((ability.Target.Length>0)?(" ("+ability.Target+")"):string.Empty);
		}
	}
	public static bool IsTank(int number) {
		if(FromGuide.TryGetValue(number,out string[] value)) {
			return value.Contains("Tank");
		}
		return false;
	}
	public static string Describe(int number) {
		return string.Join(", ",Tags(number));
	}
	public static bool Covers(int number,string wanted) {
		return Tags(number).Any((string tag) => tag.Contains(wanted,StringComparison.OrdinalIgnoreCase));
	}
}
