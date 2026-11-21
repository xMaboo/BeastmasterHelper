namespace Beastmaster;

public sealed record CrucibleFamiliar(int Row,int CatalogueId,string Name,uint HP,uint MaxHP,int Slot,FamiliarCombatProfile Profile) {
	public int FeedSlots { get; init; }
	public bool RestSelected { get; init; }
}
