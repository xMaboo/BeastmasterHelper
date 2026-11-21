using System.Collections.Generic;
using System.Numerics;
namespace Beastmaster;

public static class BeastmasterQuestGuide {
	public static IReadOnlyList<BeastmasterQuestDefinition> Quests { get; }=new BeastmasterQuestDefinition[13] {
		new BeastmasterQuestDefinition(71026u,"Strangers in the Wood","Unlocks Beastmaster and the Master's Bestiary.",50,"Excited Adventurer"),
		new BeastmasterQuestDefinition(71027u,"The Wilds Call","Beastmaster job quest.",1,"J'yhuh Tia"),
		new BeastmasterQuestDefinition(71028u,"Hearts Aligned","Beastmaster job quest.",8,"J'yhuh Tia"),
		new BeastmasterQuestDefinition(71029u,"Master of the Lhu","Beastmaster job quest.",18,"J'yhuh Tia"),
		new BeastmasterQuestDefinition(71030u,"Into the Crucible","Unlocks the First Board of the Unbroken.",30,"J'yhuh Tia"),
		new BeastmasterQuestDefinition(71031u,"Wit as Weaponry","Beastmaster job quest.",30,"J'yhuh Tia"),
		new BeastmasterQuestDefinition(71032u,"Gobsmacked","Unlocks the Second Board of the Unbroken.",40,"J'yhuh Tia"),
		new BeastmasterQuestDefinition(71033u,"Sin of Blood","Beastmaster job quest.",40,"Lauda"),
		new BeastmasterQuestDefinition(71034u,"A Beastmaster's Path","Unlocks the Third Board of the Unbroken.",50,"J'yhuh Tia"),
		new BeastmasterQuestDefinition(71035u,"Bonds Unbroken","Completes the chain; unlocks Beastmaster New Game+.",50,"Lauda"),
		new BeastmasterQuestDefinition(71036u,"Free for All","Unlocks the First Master's Board.",50,"Sylmond",IsCrucible: true),
		new BeastmasterQuestDefinition(71037u,"Mastery Rematch","Unlocks the Second Master's Board.",50,"Sylmond",IsCrucible: true),
		new BeastmasterQuestDefinition(71045u,"Blazing Trails","Unlocks Crucible Degrees and Leaderboards.",50,"Sylmond",IsCrucible: true)
	};
	public static IReadOnlyList<BeastmasterQuestTarget> Targets { get; }=new[] { new BeastmasterQuestTarget(71026u,1,148u,4u,new Vector3(-318.654f,60.947f,-129.382f),"Central Shroud","First quest objective") };
}
