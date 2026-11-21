using System;
using System.Collections.Generic;
using System.Linq;
namespace Beastmaster;

public static class CrucibleShopPlan {
	public static readonly string[] PriorityNames=new string[2] { "Tome of the Impervious", "Crucible Feather" };
	private const int LeftoverLimit=2;
	private static (int Minimum,int Maximum) Quota(CrucibleShopTier tier,int healingWanted,int playerLimit,int familiarTarget) {
		return tier switch {
			CrucibleShopTier.Healing => (Minimum: 0,Maximum: Math.Max(0,healingWanted)+3),
			CrucibleShopTier.Priority => (Minimum: 0,Maximum: PriorityNames.Length),
			CrucibleShopTier.PlayerGear => (Minimum: 1,Maximum: Math.Max(0,playerLimit)),
			CrucibleShopTier.FamiliarBuff => (Minimum: 2,Maximum: Math.Max(0,familiarTarget)),
			_ => (Minimum: 0,Maximum: 2),
		};
	}
	public static CrucibleShopTier TierOf(CrucibleItem item) {
		if(item.IsRingOfCuring) {
			return CrucibleShopTier.Healing;
		}
		if(item.IsVampiricEssence) {
			return CrucibleShopTier.Healing;
		}
		if(item.IsAutoPotionKit) {
			return CrucibleShopTier.Healing;
		}
		if(PriorityNames.Any((string name) => string.Equals(name,item.Name,StringComparison.OrdinalIgnoreCase))) {
			return CrucibleShopTier.Priority;
		}
		if(item.IsHealingStock) {
			if(item.HealPercent<20) {
				return CrucibleShopTier.Leftover;
			}
			return CrucibleShopTier.Healing;
		}
		if(item.Kind==CrucibleItemKind.Feed) {
			return CrucibleShopTier.FamiliarBuff;
		}
		if(item.Kind!=CrucibleItemKind.BeastGear) {
			return CrucibleShopTier.Leftover;
		}
		return CrucibleShopTier.PlayerGear;
	}
	public static List<CrucibleShopOffer> Order(IEnumerable<CrucibleShopOffer> offers,int healingStockHeld,int healingStockWanted,int playerBuyLimit=2,int familiarBuffTarget=4,int tokens=-1) {
		List<CrucibleShopOffer> list=offers.Where((CrucibleShopOffer crucibleShopOffer) => crucibleShopOffer.Price>=0&&!crucibleShopOffer.Item.IsUnusedByPlugin).ToList();
		List<CrucibleShopOffer> ordered=new List<CrucibleShopOffer>();
		int healingWanted=Math.Max(0,healingStockWanted-healingStockHeld);
		int num=((tokens<0)?int.MaxValue:tokens);
		CrucibleShopTier[] array=new CrucibleShopTier[5] {
			CrucibleShopTier.Healing,
			CrucibleShopTier.Priority,
			CrucibleShopTier.PlayerGear,
			CrucibleShopTier.FamiliarBuff,
			CrucibleShopTier.Leftover
		};
		CrucibleShopTier[] array2=array;
		foreach(CrucibleShopTier tier in array2) {
			(int Minimum,int Maximum) tuple=Quota(tier,healingWanted,playerBuyLimit,familiarBuffTarget);
			int item=tuple.Minimum;
			int item2=tuple.Maximum;
			int num3=0;
			foreach(CrucibleShopTier lower in array.Where((CrucibleShopTier t) => t>tier&&tier!=CrucibleShopTier.Healing)) {
				int item3=Quota(lower,healingWanted,playerBuyLimit,familiarBuffTarget).Minimum;
				num3+=(from o in list
					where TierOf(o.Item)==lower&&!ordered.Contains(o)&&o.Price>0
					orderby o.Price
					select o).Take(item3).Sum((CrucibleShopOffer o) => o.Price);
			}
			int num4=0;
			foreach(CrucibleShopOffer offer in Rank(list,tier)) {
				if(num4>=item2) {
					break;
				}
				int count=Math.Max(0,item-num4-1);
				int num5=(from o in list
					where TierOf(o.Item)==tier&&!ordered.Contains(o)&&o!=offer&&o.Price>0
					orderby o.Price
					select o).Take(count).Sum((CrucibleShopOffer o) => o.Price);
				if(num==int.MaxValue||offer.Price<=num-num3-num5) {
					ordered.Add(offer);
					num-=Math.Max(0,offer.Price);
					num4++;
				}
			}
		}
		return ordered;
	}
	private static IEnumerable<CrucibleShopOffer> Rank(IEnumerable<CrucibleShopOffer> shelf,CrucibleShopTier tier) {
		IEnumerable<CrucibleShopOffer> source=shelf.Where((CrucibleShopOffer o) => TierOf(o.Item)==tier);
		return tier switch {
			CrucibleShopTier.Healing => (IEnumerable<CrucibleShopOffer>)(from o in source
				orderby o.Item.IsRingOfCuring descending,o.Item.IsVampiricEssence descending,o.Item.HealPercent descending,o.Discounted descending
				select o),
			CrucibleShopTier.Priority => from o in source
				orderby Array.FindIndex(PriorityNames,(string name) => string.Equals(name,o.Item.Name,StringComparison.OrdinalIgnoreCase)),o.Discounted descending
				select o,
			_ => from o in source
				orderby o.Item.BonusScore descending,o.Discounted descending,o.Price
				select o,
		};
	}
	public static string Reason(CrucibleShopOffer offer) {
		CrucibleShopTier crucibleShopTier=TierOf(offer.Item);
		string text=(offer.Discounted?", discounted":string.Empty);
		switch(crucibleShopTier) {
		case CrucibleShopTier.Healing:
		if(offer.Item.IsRingOfCuring) {
			return "restores 25% of maximum HP after every battle - the best heal over a board"+text;
		}
		if(offer.Item.IsVampiricEssence) {
			return offer.Item.Summary+" - a heal that lasts the fight"+text;
		}
		return $"restores {offer.Item.HealPercent}% - the stock a run is finished with{text}";
		case CrucibleShopTier.Priority:
		return offer.Item.Summary+text;
		case CrucibleShopTier.FamiliarBuff:
		if(offer.Item.Bonuses.Count>0) {
			return string.Join(", ",offer.Item.Bonuses)+text;
		}
		break;
		}
		return (offer.Item.Bonuses.Count>0)?(string.Join(", ",offer.Item.Bonuses)+text):(offer.Item.Summary+text);
	}
}
