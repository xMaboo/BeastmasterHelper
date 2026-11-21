using System.Collections.Generic;
namespace Beastmaster;

public static class BeastmasterGuide {
	public static IReadOnlyList<BeastmasterStage> Stages { get; }=new BeastmasterStage[2] {
		new BeastmasterStage("sample-introduction","Sample stage one: meeting the Beastmaster","Only for testing the stage and manual-progress features; it does not reflect real game flow.",new BeastmasterObjective[3] {
			new BeastmasterObjective("sample-introduction-read","Read the sample description",BeastmasterObjectiveType.Explore,"Note that the data shown here is temporary sample content."),
			new BeastmasterObjective("sample-introduction-npc","Speak with the sample NPC",BeastmasterObjectiveType.TalkToNpc,"Tick manually to test individual objective progress."),
			new BeastmasterObjective("sample-introduction-quest","Complete the sample quest",BeastmasterObjectiveType.Quest,"This quest name and flow are placeholder content.")
		}),
		new BeastmasterStage("sample-training","Sample stage two: basic training","Only for verifying stage selection and independent completion.",new BeastmasterObjective[3] {
			new BeastmasterObjective("sample-training-monster","Defeat the sample monster",BeastmasterObjectiveType.Monster,"Does not track kills automatically - tick entries manually."),
			new BeastmasterObjective("sample-training-item","Obtain the sample item",BeastmasterObjectiveType.Item,"Does not scan your bags - tick entries manually."),
			new BeastmasterObjective("sample-training-duty","Complete the sample challenge",BeastmasterObjectiveType.Duty,"Does not read duty state - tick entries manually.")
		})
	};
}
