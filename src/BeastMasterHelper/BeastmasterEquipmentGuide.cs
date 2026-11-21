using System.Collections.Generic;
namespace Beastmaster;

public static class BeastmasterEquipmentGuide {
	public const string SlotMainHand="Main Hand";
	public const string SlotOffHand="Off Hand";
	public const string SlotHead="Head";
	public const string SlotBody="Body";
	public const string SlotHands="Hands";
	public const string SlotLegs="Legs";
	public const string SlotFeet="Feet";
	public const string SlotEarrings="Earrings";
	public const string SlotNecklace="Necklace";
	public const string SlotBracelets="Bracelets";
	public const string SlotRing1="Ring 1";
	public const string SlotRing2="Ring 2";
	public const string SlotSoulCrystal="Soul Crystal";
	public const string SlotFood="Food";
	public const string CategoryWeapon="Weapon";
	public const string CategoryShield="Shield";
	public const string CategoryArmour="Armour";
	public const string CategoryAccessory="Accessory";
	public const string CategoryJob="Job";
	public const string CategoryFood="Food";
	public const string NoEquipment="No equipment";
	public static IReadOnlyList<BeastmasterEquipmentEntry> Level50Starter { get; }=new BeastmasterEquipmentEntry[13] {
		new BeastmasterEquipmentEntry("Main Hand","Beastmaster's Hand Axe","Weapon",50742u),
		new BeastmasterEquipmentEntry("Off Hand","Beastmaster's Hoplon","Shield",50743u),
		new BeastmasterEquipmentEntry("Head","Woolen Turban","Armour",2810u),
		new BeastmasterEquipmentEntry("Body","Woolen Shirt","Armour",3147u),
		new BeastmasterEquipmentEntry("Hands","Dreadwyrm Bracers of Striking","Armour",8961u),
		new BeastmasterEquipmentEntry("Legs","Woolen Sarouel","Armour",3408u),
		new BeastmasterEquipmentEntry("Feet","Dreadwyrm Boots of Striking","Armour",8963u),
		new BeastmasterEquipmentEntry("Earrings","Aetheryte Earring","Accessory",24589u),
		new BeastmasterEquipmentEntry("Necklace","Dreadwyrm Choker of Slaying","Accessory",8967u),
		new BeastmasterEquipmentEntry("Bracelets","No equipment","Accessory"),
		new BeastmasterEquipmentEntry("Ring 1","Neophyte's Ring","Accessory",44410u),
		new BeastmasterEquipmentEntry("Ring 2","Augmented Ironworks Ring of Slaying","Accessory",8901u),
		new BeastmasterEquipmentEntry("Soul Crystal","Soul of the Beastmaster","Job",47474u)
	};
	public static IReadOnlyList<BeastmasterEquipmentEntry> Level50BestInSlot { get; }=new BeastmasterEquipmentEntry[13] {
		new BeastmasterEquipmentEntry("Main Hand","Beastliege's Hand Axe","Weapon",51736u),
		new BeastmasterEquipmentEntry("Off Hand","Beastliege's Hoplon","Shield",51737u),
		new BeastmasterEquipmentEntry("Head","Beastmaster's Horns +4","Armour",50772u),
		new BeastmasterEquipmentEntry("Body","Beastmaster's Furs +4","Armour",50773u),
		new BeastmasterEquipmentEntry("Hands","Beastmaster's Armguards +4","Armour",50774u),
		new BeastmasterEquipmentEntry("Legs","Beastmaster's Slops +4","Armour",50775u),
		new BeastmasterEquipmentEntry("Feet","Beastmaster's Boots +4","Armour",50776u),
		new BeastmasterEquipmentEntry("Earrings","Beastmaster's Earrings","Accessory",51745u),
		new BeastmasterEquipmentEntry("Necklace","Dreadwyrm Choker of Slaying","Accessory",8967u),
		new BeastmasterEquipmentEntry("Bracelets","Augmented Ironworks Bracelet of Slaying","Accessory",8898u),
		new BeastmasterEquipmentEntry("Ring 1","Augmented Ironworks Ring of Slaying","Accessory",8901u),
		new BeastmasterEquipmentEntry("Ring 2","Dreadwyrm Ring of Slaying","Accessory",8968u),
		new BeastmasterEquipmentEntry("Food","Rock-fisted Popoto Stew","Food",49245u)
	};
}
