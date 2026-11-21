using System;
using System.Collections.Generic;
namespace Beastmaster;

public sealed record CrucibleItem(int Id,CrucibleItemKind Kind,uint IconId,int Value,string Name,string Effect,string Summary) {
	public int HealPercent { get; init; }
	public bool IsReraiser {
		get {
			int id=Id;
			if((uint)(id-98)<=1u) {
				return true;
			}
			return false;
		}
	}
	public bool IsLowGradeHealing {
		get {
			if(IsHealingStock) {
				return HealPercent<=10;
			}
			return false;
		}
	}
	public bool IsFang {
		get {
			int id=Id;
			if(id>=128) {
				return id<=134;
			}
			return false;
		}
	}
	public bool IsTemporalSand => Id==138;
	public bool IsElementalWeakener {
		get {
			int id=Id;
			if(id>=116) {
				return id<=127;
			}
			return false;
		}
	}
	public bool IsResistanceSerum {
		get {
			int id=Id;
			if(id>=86) {
				return id<=95;
			}
			return false;
		}
	}
	public bool IsStatusCure {
		get {
			int id=Id;
			if(id==83||id==85) {
				return true;
			}
			return false;
		}
	}
	public bool IsNeedle => Id==84;
	public bool IsCelestialSand => Id==139;
	public bool IsReraiserItem {
		get {
			int id=Id;
			if((uint)(id-98)<=1u) {
				return true;
			}
			return false;
		}
	}
	public bool IsFamiliarRevive => Id==96;
	public bool IsDamageTypeTome {
		get {
			int id=Id;
			if((uint)(id-142)<=1u) {
				return true;
			}
			return false;
		}
	}
	public bool IsEscape => Id==97;
	public bool IsEye {
		get {
			int id=Id;
			if((uint)(id-100)<=1u) {
				return true;
			}
			return false;
		}
	}
	public bool IsTimedCombatBuff {
		get {
			int id=Id;
			if(id>=102&&id<=114) {
				return Id!=115;
			}
			return false;
		}
	}
	public bool IsCrucibleFeather => Id==115;
	public bool IsBlinkTome => Id==137;
	public bool IsReflectTome => Id==136;
	public bool IsAutoPotionKit => Id==140;
	public bool IsRingOfCuring => Id==3;
	public bool IsAutoRemedyKit => Id==141;
	public bool IsMerchantsEye => Id==101;
	public bool IsThiefsEye => Id==100;
	public bool IsVampiricEssence => Id==135;
	public bool IsUnusedByPlugin {
		get {
			if(!IsElementalWeakener&&!IsResistanceSerum&&!IsStatusCure&&!IsNeedle&&!IsEscape&&!IsTemporalSand&&!IsTimedCombatBuff&&!IsReraiserItem&&!IsFamiliarRevive&&!IsAutoRemedyKit) {
				return IsDamageTypeTome;
			}
			return true;
		}
	}
	public int BuffSeconds { get; init; }
	public string Singular { get; init; }=string.Empty;
	public string Plural { get; init; }=string.Empty;
	public bool HealsArea { get; init; }
	public bool Heals => HealPercent>0;
	public IReadOnlyList<CrucibleBonus> Bonuses { get; init; }=Array.Empty<CrucibleBonus>();
	public float BonusScore { get; init; }
	public bool IsHealingStock {
		get {
			if(Kind==CrucibleItemKind.CrucibleItem&&HealPercent>0&&Bonuses.Count==0&&!IsAutoPotionKit) {
				return !IsNeedle;
			}
			return false;
		}
	}
	public bool IsNamed(string text) {
		string text2=text.Trim().TrimEnd('.');
		if(text2.Length>0) {
			if(!string.Equals(Name,text2,StringComparison.OrdinalIgnoreCase)&&!string.Equals(Singular,text2,StringComparison.OrdinalIgnoreCase)) {
				return string.Equals(Plural,text2,StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}
}
