namespace Beastmaster;

public sealed record BeastmasterQuestDefinition(uint RowId,string Name,string Summary,int Level=0,string Giver="",bool IsCrucible=false) {
	public string DisplayName => BeastmasterGameData.QuestName(RowId,Name);
}
