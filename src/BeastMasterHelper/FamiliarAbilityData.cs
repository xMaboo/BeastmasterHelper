using System.Collections.Generic;
using System.Linq;
namespace Beastmaster;

public static class FamiliarAbilityData {
	private static readonly Dictionary<int,FamiliarInfo> ByNumber=Build();
	public static IReadOnlyCollection<FamiliarInfo> All => ByNumber.Values;
	public static FamiliarInfo? Get(int number) {
		if(!ByNumber.TryGetValue(number,out FamiliarInfo value)) {
			return null;
		}
		return value;
	}
	private static Dictionary<int,FamiliarInfo> Build() {
		return new FamiliarInfo[50] {
			F(1,"Cu Sith",BeastKin.Beastkin,"Piercing","Relentless Rake","Delivers a sixfold slashing physical attack.","Single Target","Rake","Delivers a slashing physical attack.","Area of Effect","Beastskin"),
			F(2,"Squirrel",BeastKin.Beastkin,"Fire","Scamper","Hastens allies.","Area of Effect","Somersault Slash","Deals fire-aspected damage.","Area of Effect","Beastskin"),
			F(3,"Lamb",BeastKin.Beastkin,"Blunt","Lullaby","Puts enemies to sleep and improves sleep resistance of allies.","Area of Effect","Fleece Butt","Delivers a blunt physical attack.","Area of Effect","Beastskin"),
			F(4,"Pugil",BeastKin.Wavekin,"Water","Water Wall","Reduces magic vulnerability and improves paralysis resistance of allies.","Area of Effect","Screwdriver","Deals water-aspected damage.","Area of Effect","Quelling Wave"),
			F(5,"Opo-opo",BeastKin.Beastkin,"Blunt","Pins and Nails","Deals unaspected damage that paralyzes enemy.","Single Target","Stone Throw","Delivers a blunt physical attack.","Area of Effect","Beastskin"),
			F(6,"Dodo",BeastKin.Cloudkin,"Earth","Strut","Improves earth resistance and blind resistance of allies.","Area of Effect","Fowl Stench","Deals earth-aspected damage that blinds enemies.","Area of Effect","Cloud Skim"),
			F(7,"Coblyn",BeastKin.Soulkin,"Lightning","Vulcanize","Reduces physical vulnerability and improves poison resistance of allies.","Area of Effect","Bestial Thunder","Deals lightning-aspected damage.","Single Target","Soul Crush"),
			F(8,"Diremite",BeastKin.Vilekin,"Piercing","Silkscreen","Deals unaspected damage that knocks back and binds enemies.","Area of Effect","Deadly Thrust","Delivers a piercing physical attack that inflicts poison on enemy.","Single Target","Vileskin"),
			F(9,"Megalocrab",BeastKin.Wavekin,"Water","Bubble Shower","Deals water-aspected damage and lowers water resistance of enemies.","Area of Effect","Drenching Blow","Deals water-aspected damage.","Area of Effect","Quelling Wave"),
			F(10,"Wespe",BeastKin.Vilekin,"Piercing","Final Sting","Delivers a piercing physical attack on enemy. Familiar retreats upon execution.","Single Target","Sharp Sting","Delivers a piercing physical attack that inflicts poison on enemy.","Single Target","Vileskin"),
			F(11,"Vulture",BeastKin.Cloudkin,"Wind","Bloodcurdling Caw","Deals wind-aspected damage and dispels one beneficial status from enemies.","Area of Effect","Wing Cutter","Deals wind-aspected damage.","Area of Effect","Cloud Skim"),
			F(12,"Mandragora",BeastKin.Seedkin,"Blunt","Heirloom Scream","Deals unaspected damage and inflicts heaviness on enemies.","Area of Effect","Budbutt","Delivers a blunt physical attack.","Single Target","Seedsower"),
			F(13,"Geshunpest",BeastKin.Ashkin,"Lightning","Odium","Grants HP absorption to allies.","Area of Effect","Dark Thunder","Deals lightning-aspected damage.","Area of Effect","Scouring Ash"),
			F(14,"Puk",BeastKin.Scalekin,"Piercing","Tail Chase","Delivers a slashing physical attack that knocks back enemies.","Area of Effect","Fireball","Deals fire-aspected damage.","Area of Effect","Scaleskin"),
			F(15,"Crab",BeastKin.Wavekin,"Water","Hundred Fists","Hastens self.","Single Target","Bubble Shower","Deals water-aspected damage.","Area of Effect","Quelling Wave"),
			F(16,"Mantis",BeastKin.Vilekin,"Slashing","Eerie Soundwave","Deals unaspected damage that increases physical vulnerability of enemies.","Area of Effect","Standing Chine","Delivers a slashing physical attack.","Single Target","Vileskin"),
			F(17,"Slime",BeastKin.Ashkin,"Unaspected","Syrup","Deals unaspected damage that binds enemies.","Area of Effect","Digest","Deals unaspected damage that generates beastmaster HP.","Single Target","Scouring Ash"),
			F(18,"Dullahan",BeastKin.Soulkin,"Slashing","King's Will","Increases physical damage dealt by allies.","Area of Effect","Iron Justice","Delivers a slashing physical attack.","Area of Effect","Soul Crush"),
			F(19,"Bat",BeastKin.Cloudkin,"Unaspected","Ultrasonics","Deals unaspected damage, reduces enemy accuracy, and removes a status ailment from allies.","Area of Effect","Blood Drain","Deals unaspected damage that generates beastmaster HP.","Single Target","Cloud Skim"),
			F(20,"Flying Trap",BeastKin.Seedkin,"Ice","Necrotic Nectar","Deals unaspected damage and increases magic vulnerability of enemies.","Area of Effect","Sour Sough","Deals wind-aspected damage that knocks back and poisons enemies.","Area of Effect","Seedsower"),
			F(21,"Ziz",BeastKin.Scalekin,"Ice","Petribreath","Deals unaspected damage that petrifies enemies.","Area of Effect","Ice Breath","Deals ice-aspected damage.","Area of Effect","Scaleskin"),
			F(22,"Sabotender",BeastKin.Seedkin,"Piercing","Hypodermic Hustle","Grants a keen edge to allies.","Area of Effect","Natural Needles","Delivers a piercing physical attack.","Area of Effect","Seedsower"),
			F(23,"Golem",BeastKin.Soulkin,"Earth","Rockslide","Deals earth-aspected damage and lowers earth resistance of enemies.","Area of Effect","Boulder Clap","Deals earth-aspected damage.","Area of Effect","Soul Crush"),
			F(24,"Apkallu",BeastKin.Cloudkin,"Piercing","Regurgitate","Deals water-aspected damage that slows enemy.","Single Target","Sardine","Deals water-aspected damage.","Single Target","Cloud Skim"),
			F(25,"Adamantoise",BeastKin.Scalekin,"Lightning","Harden Shell","Reduces physical vulnerability and improves petrification resistance of allies.","Area of Effect","Bestial Thunder II","Deals lightning-aspected damage.","Area of Effect","Scaleskin"),
			F(26,"Buffalo",BeastKin.Beastkin,"Blunt","War Cry","Deals unaspected damage that stuns enemies.","Area of Effect","Heave","Delivers a blunt physical attack.","Area of Effect","Beastskin"),
			F(27,"Uragnite",BeastKin.Wavekin,"Ice","Gas Shell","Deals unaspected damage that poisons enemies.","Area of Effect","Frost Breath (Uragnite)","Deals ice-aspected damage.","Area of Effect","Quelling Wave"),
			F(28,"Worm",BeastKin.Vilekin,"Earth","Bottomless Desert","Deals earth-aspected damage and inflicts heaviness on enemies.","Area of Effect","Sand Breath","Deals earth-aspected damage that blinds enemies.","Area of Effect","Vileskin"),
			F(29,"Spriggan",BeastKin.Soulkin,"Fire","Frenetic Flurry","Delivers a sevenfold blunt physical attack.","Single Target","Romp","Deals fire-aspected damage.","Area of Effect","Soul Crush"),
			F(30,"Goobbue",BeastKin.Beastkin,"Blunt","Moldy Sneeze","Deals unaspected damage that sickens enemies.","Area of Effect","Beatdown","Delivers a blunt physical attack.","Area of Effect","Beastskin"),
			F(31,"Gigantoad",BeastKin.Wavekin,"Blunt","Sticky Tongue","Delivers a blunt physical attack that draws in and reduces accuracy of enemy.","Single Target","Bestial Blizzard II","Deals ice-aspected damage.","Area of Effect","Quelling Wave"),
			F(32,"Colibri",BeastKin.Cloudkin,"Piercing","Pecking Flurry","Delivers a fivefold piercing physical attack.","Single Target","Loop","Delivers a piercing physical attack.","Single Target","Cloud Skim"),
			F(33,"Coeurl",BeastKin.Beastkin,"Lightning","Charged Whisker","Deals lightning-aspected damage that paralyzes and lowers lightning resistance of enemies.","Area of Effect","Blaster","Deals lightning-aspected damage.","Single Target","Beastskin"),
			F(34,"Raptor",BeastKin.Scalekin,"Ice","Foul Breath","Deals fire-aspected damage and lowers fire resistance of enemies.","Area of Effect","Frost Breath (Raptor)","Deals ice-aspected damage.","Area of Effect","Scaleskin"),
			F(35,"Drake",BeastKin.Scalekin,"Fire","Smoldering Scales","Reduces magic vulnerability and improves fire resistance of allies.","Area of Effect","Burning Cyclone","Deals fire-aspected damage.","Area of Effect","Scaleskin"),
			F(36,"Treant",BeastKin.Seedkin,"Earth","Arboreal Storm","Deals wind-aspected damage that knocks back enemies.","Area of Effect","Acorn Bomb","Deals earth-aspected damage that causes nightmares.","Area of Effect","Seedsower"),
			F(37,"Antling",BeastKin.Vilekin,"Slashing","Formic Pheromones","Slows enemies and improves water, earth, and ice resistance of allies.","Area of Effect","Mandible Bite","Delivers a piercing physical attack.","Single Target","Vileskin"),
			F(38,"Chimera",BeastKin.Beastkin,"Slashing","The Ram's Voice","Deals ice-aspected damage that deeply freezes and lowers ice resistance of enemies.","Area of Effect","The Lion's Breath","Deals fire-aspected damage particularly effective against deeply frozen enemies.","Area of Effect","Beastskin"),
			F(39,"Morbol",BeastKin.Seedkin,"Blunt","Bad Breath","Deals unaspected damage that slows, blinds, and paralyzes enemies.","Area of Effect","Vine Probe","Delivers a piercing physical attack.","Area of Effect","Seedsower"),
			F(40,"Ghost",BeastKin.Ashkin,"Wind","Curse","Deals unaspected damage that may doom enemies. Inflicts additional damage upon diseased enemies.","Area of Effect","Fell Gale","Deals wind-aspected damage that may doom enemies.","Area of Effect","Scouring Ash"),
			F(41,"Salamander",BeastKin.Wavekin,"Water","Peculiar Light","Deals unaspected damage that increases magic vulnerability of enemies.","Area of Effect","Brackish Rain","Deals water-aspected damage.","Area of Effect","Quelling Wave"),
			F(42,"Cobra",BeastKin.Scalekin,"Piercing","Stone Gaze","Deals unaspected damage that petrifies enemy.","Single Target","Dripping Fang","Delivers a piercing physical attack that poisons enemy.","Single Target","Scaleskin"),
			F(43,"Hydra",BeastKin.Scalekin,"Slashing","White Breath","Deals unaspected damage that sickens enemies.","Area of Effect","Main Trap","Delivers a piercing physical attack.","Single Target","Scaleskin"),
			F(44,"Damselfly",BeastKin.Vilekin,"Wind","Venom","Deals unaspected damage that poisons enemies.","Area of Effect","Cursed Sphere","Deals wind-aspected damage.","Area of Effect","Vileskin"),
			F(45,"Rotting Goobbue",BeastKin.Ashkin,"Earth","Inhale","Deals unaspected damage that draws in enemies.","Area of Effect","Dirty Sneeze","Deals earth-aspected damage.","Single Target","Scouring Ash"),
			F(46,"Zu",BeastKin.Cloudkin,"Slashing","Breath Wing","Deals wind-aspected damage and lowers wind resistance of enemies.","Area of Effect","Flying Frenzy","Rushes enemy and delivers a blunt physical attack.","Area of Effect","Cloud Skim"),
			F(47,"Ice Golem",BeastKin.Soulkin,"Ice","Frozen Heart","Increases magic damage dealt by allies.","Area of Effect","Ice Guillotine","Deals ice-aspected damage.","Area of Effect","Soul Crush"),
			F(48,"Karlabos",BeastKin.Wavekin,"Water","Tail Screw","Deals water-aspected damage. Occasionally reduces enemy HP to a single digit.","","Impale","Delivers a piercing physical attack.","Single Target","Quelling Wave"),
			F(49,"Rafflesia",BeastKin.Seedkin,"Piercing","Blighted Bouquet","Deals unaspected damage that may doom enemies.","Area of Effect","Bloody Caress","Deals wind-aspected damage.","Area of Effect","Seedsower"),
			F(50,"Behemoth",BeastKin.Beastkin,"Lightning","Meteor","Deals fire-aspected damage.","Area of Effect","Thunderbolt","Deals lightning-aspected damage.","Area of Effect","Beastskin")
		}.ToDictionary((FamiliarInfo f) => f.Number);
		static FamiliarInfo F(int n,string name,BeastKin kin,string auto,string tn,string te,string tt,string kn,string ke,string kt,string borrow) {
			return new FamiliarInfo(n,name,kin,auto,new FamiliarAbility(tn,te,tt),new FamiliarAbility(kn,ke,kt),borrow);
		}
	}
}
