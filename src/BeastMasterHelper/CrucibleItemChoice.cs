using System;
using System.Collections.Generic;
using System.Linq;
namespace Beastmaster;

public static class CrucibleItemChoice {
	public readonly record struct CrucibleHealChoice(CrucibleItem? Item,string Reason,bool ForFamiliar=false) {
		public bool HasItem => Item!=null;
		public static readonly CrucibleHealChoice None=new CrucibleHealChoice(null,"Nothing worth using");
	}
	public static CrucibleHealChoice HealEither(IEnumerable<CrucibleItem> available,float playerHpPercent,float familiarHpPercent,int playerPotionReserve=int.MaxValue) {
		List<CrucibleItem> list=available.Where((CrucibleItem item) => item.Heals&&!item.IsAutoPotionKit).ToList();
		if(list.Count==0) {
			return new CrucibleHealChoice(null,"Nothing held that heals");
		}
		List<CrucibleItem> items=list.Where((CrucibleItem item) => item.HealsArea).ToList();
		List<CrucibleItem> list2=list.Where((CrucibleItem item) => !item.HealsArea).ToList();
		bool flag=familiarHpPercent>=0f;
		float num=Math.Max(0f,100f-playerHpPercent);
		float num2=(flag?Math.Max(0f,100f-familiarHpPercent):0f);
		if(flag&&list2.Count>playerPotionReserve) {
			CrucibleItem crucibleItem=Fitted(list2,num2);
			if(crucibleItem!=null) {
				return new CrucibleHealChoice(crucibleItem,$"{crucibleItem.Name} to the familiar - it is missing {num2:0}% and {list2.Count} Beast Potions are held against a reserve of {playerPotionReserve}, so this one is spare",ForFamiliar: true);
			}
		}
		if(flag) {
			CrucibleItem crucibleItem2=Fitted(items,num2);
			if(crucibleItem2!=null) {
				string value=((num>0f)?$", and you {num:0}%":string.Empty);
				return new CrucibleHealChoice(crucibleItem2,$"{crucibleItem2.Name} restores {crucibleItem2.HealPercent}% to you and nearby allies - the familiar is missing {num2:0}%{value}, and this is the largest Ash that wastes none of it");
			}
		}
		CrucibleItem crucibleItem3=Fitted(list2,num);
		if(crucibleItem3!=null) {
			return new CrucibleHealChoice(crucibleItem3,$"{crucibleItem3.Name} restores {crucibleItem3.HealPercent}% and you are missing {num:0}% - the largest Beast Potion that wastes none of it");
		}
		CrucibleItem crucibleItem4=Fitted(items,num);
		if(crucibleItem4!=null) {
			return new CrucibleHealChoice(crucibleItem4,$"{crucibleItem4.Name} restores {crucibleItem4.HealPercent}% - you are missing {num:0}% and no Beast Potion fits it, so this is the largest Ash that "+"wastes none of it");
		}
		string value2=(flag?$", familiar {familiarHpPercent:0}%":string.Empty);
		return new CrucibleHealChoice(null,$"HP is {playerHpPercent:0}%{value2} - nothing held is small enough to be absorbed by "+"what either of you is missing, so none of it is worth spending yet");
	}
	private static CrucibleItem? Fitted(IEnumerable<CrucibleItem> items,float missing) {
		return (from item in items
			where (float)item.HealPercent<=missing
			orderby item.HealPercent descending
			select item).FirstOrDefault();
	}
	public static List<CrucibleItemPick> ShopPlan(IEnumerable<CrucibleItem> offered,int healingStockHeld,int healingStockWanted=3) {
		List<CrucibleItemPick> list=new List<CrucibleItemPick>();
		List<CrucibleItem> list2=offered.Where((CrucibleItem item) => item!=null).ToList();
		if(list2.Count==0) {
			return list;
		}
		int num=Math.Max(0,healingStockWanted-healingStockHeld);
		foreach(CrucibleItem item in from i in list2
			where i.IsHealingStock
			orderby i.HealPercent descending
			select i) {
			if(num--<=0) {
				break;
			}
			list.Add(new CrucibleItemPick(item,$"{item.Name} restores {item.HealPercent}% and is cheap - stocking up for a later fight"));
		}
		foreach(CrucibleItem item2 in from i in list2
			where !i.IsHealingStock
			orderby i.BonusScore descending,i.Value descending
			select i) {
			string reason=((item2.Bonuses.Count>0)?(item2.Name+": "+string.Join(", ",item2.Bonuses)):(item2.Name+": "+item2.Summary));
			list.Add(new CrucibleItemPick(item2,reason));
		}
		return list;
	}
	public static bool AnyoneCanReceive(IReadOnlyList<CrucibleFamiliar> familiars,CrucibleItem? item,float minimumHpPercent=0f) {
		if(item==null||familiars.Count==0) {
			return true;
		}
		return FeedTarget(familiars,item,minimumHpPercent)!=null;
	}
	public static List<CrucibleItemPick> TreasurePlan(IEnumerable<CrucibleItem> offered,int healingStockHeld,int healingStockWanted,bool hurt) {
		return (from entry in Rank(offered,healingStockHeld,healingStockWanted,hurt)
			select new CrucibleItemPick(entry.Item,TreasureReason(entry.Item,entry.Score))).ToList();
	}
	public static string DescribeTreasurePlan(IEnumerable<CrucibleItem> offered,int healingStockHeld,int healingStockWanted,bool hurt) {
		List<(CrucibleItem,float)> list=Rank(offered,healingStockHeld,healingStockWanted,hurt).ToList();
		if(list.Count==0) {
			return "No treasure cards to rank.";
		}
		return "Treasure ranking: "+string.Join(" > ",list.Select(((CrucibleItem Item,float Score) e) => $"{e.Item.Name} {e.Score:0.#}{(e.Item.IsReraiser?" (last priority)":"")}"));
	}
	private static IEnumerable<(CrucibleItem Item,float Score)> Rank(IEnumerable<CrucibleItem> offered,int healingStockHeld,int healingStockWanted,bool hurt) {
		return from item in offered
			where item!=null&&!item.IsUnusedByPlugin
			select (Item: item,Score: TreasureScore(item,healingStockHeld,healingStockWanted,hurt)) into entry
			orderby entry.Item.IsReraiser,entry.Item.IsLowGradeHealing,entry.Score descending,entry.Item.Value descending
			select entry;
	}
	private static float TreasureScore(CrucibleItem item,int healingStockHeld,int healingStockWanted,bool hurt) {
		if(item.IsHealingStock) {
			int num=Math.Max(0,healingStockWanted-healingStockHeld);
			return (float)item.HealPercent*((num>0)?1f:0.25f)*(hurt?2f:1f);
		}
		float num2=CrucibleGearMerit.Of(item);
		float num3=((item.Bonuses.Count>0)?(item.BonusScore*DurationDiscount(item)):0f);
		if(num3>0f||num2>0f) {
			return num3+num2;
		}
		return (float)item.Value*0.25f;
	}
	private static float DurationDiscount(CrucibleItem item) {
		if(item.Kind==CrucibleItemKind.BeastGear||item.BuffSeconds<=0) {
			return 1f;
		}
		if(item.BuffSeconds<60) {
			return 0.1f;
		}
		return 0.25f;
	}
	private static string TreasureReason(CrucibleItem item,float score) {
		if(item.IsReraiser) {
			return item.Name+": last priority by preference; choose only after all non-Reraise options";
		}
		if(item.IsLowGradeHealing) {
			return $"{item.Name}: low priority; restores only {item.HealPercent}%";
		}
		string text=CrucibleGearMerit.Reason(item);
		if(item.Bonuses.Count>0) {
			string text2=string.Join(", ",item.Bonuses);
			if(text.Length!=0) {
				return $"{item.Name}: {text2}; {text}";
			}
			return item.Name+": "+text2;
		}
		if(text.Length>0) {
			return item.Name+": "+text;
		}
		if(item.IsHealingStock) {
			return $"{item.Name} restores {item.HealPercent}% (worth {score:0})";
		}
		if(!string.IsNullOrWhiteSpace(item.Summary)) {
			return item.Name+": "+item.Summary;
		}
		return $"{item.Name}, valued at {item.Value} by the item sheet";
	}
	public static CrucibleFamiliar? FeedTarget(IEnumerable<CrucibleFamiliar> familiars,CrucibleItem? item,float minimumHpPercent=0f) {
		List<CrucibleFamiliar> list=familiars.Where((CrucibleFamiliar familiar) => CrucibleFeedSuitability.CanReceive(item,familiar)&&CrucibleFeedFeedback.Shared.HasRoom(familiar.Name,familiar.FeedSlots)&&!CrucibleFeedFeedback.Shared.HasEaten(familiar.Name,item)).ToList();
		CrucibleFamiliar crucibleFamiliar=FeedTarget(list.Where((CrucibleFamiliar familiar) => familiar.MaxHP==0||(float)familiar.HP*100f/(float)familiar.MaxHP>=minimumHpPercent).ToList());
		if(crucibleFamiliar!=null) {
			return crucibleFamiliar;
		}
		return FeedTarget(list);
	}
	public static CrucibleFamiliar? ConsumableTarget(IEnumerable<CrucibleFamiliar> familiars) {
		return (from familiar in familiars
			where familiar.MaxHP!=0
			orderby (familiar.Slot>=3)?1:0,familiar.Slot
			select familiar).FirstOrDefault();
	}
	public static CrucibleFamiliar? FeedTarget(IEnumerable<CrucibleFamiliar> familiars) {
		CrucibleFamiliar crucibleFamiliar=null;
		bool flag=false;
		float num=-1f;
		foreach(CrucibleFamiliar familiar in familiars) {
			if(familiar.MaxHP!=0) {
				bool flag2=familiar.Slot<3;
				float num2=(float)familiar.HP*100f/(float)familiar.MaxHP;
				if(crucibleFamiliar==null||(flag2&&!flag)||(flag2==flag&&num2>num)||(flag2==flag&&Math.Abs(num2-num)<0.01f&&familiar.Slot<crucibleFamiliar.Slot)) {
					crucibleFamiliar=familiar;
					flag=flag2;
					num=num2;
				}
			}
		}
		return crucibleFamiliar;
	}
	public static string FeedReason(CrucibleFamiliar? familiar,CrucibleItem? item) {
		if(familiar==null) {
			return "No familiar to feed";
		}
		float value=((familiar.MaxHP==0)?0f:((float)familiar.HP*100f/(float)familiar.MaxHP));
		string value2=((familiar.Slot<3)?$"team slot {familiar.Slot+1}":"the reserve");
		string value3=((item==null)?"Feeding":("Giving "+item.Name+" to"));
		return $"{value3} {familiar.Name} - {value2}, {value:0}% HP, the healthiest that will be fighting";
	}
}
