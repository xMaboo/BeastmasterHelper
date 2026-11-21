namespace Beastmaster;

public sealed record BeastmasterEquipmentEntry(string Slot,string Name,string Category,uint ItemId=0u) {
	public string DisplayName {
		get {
			if(ItemId!=0) {
				return BeastmasterGameData.ItemName(ItemId,Name);
			}
			return Name;
		}
	}
}
