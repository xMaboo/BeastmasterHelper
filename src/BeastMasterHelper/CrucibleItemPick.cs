namespace Beastmaster;

public readonly record struct CrucibleItemPick(CrucibleItem? Item,string Reason) {
	public bool HasItem => Item!=null;
	public static readonly CrucibleItemPick None=new CrucibleItemPick(null,"Nothing worth using");
}
