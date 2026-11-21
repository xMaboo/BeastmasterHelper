namespace Beastmaster;

public sealed record CrucibleShopOffer(int Row,CrucibleItem Item,int Price,bool Discounted) {
	public override string ToString() {
		return $"{Item.Name} {Price}{(Discounted?" (discounted)":string.Empty)}";
	}
}
