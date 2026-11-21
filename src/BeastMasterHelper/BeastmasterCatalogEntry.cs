namespace Beastmaster;

public sealed record BeastmasterCatalogEntry(int Number,string Name,BeastmasterCatalogLocationType LocationType,string Location,ushort TerritoryType=0,float? MapX=null,float? MapY=null,string Level="",ushort MapRowId=0,float? WorldX=null,float? WorldY=null,float? WorldZ=null,uint ContentFinderConditionId=0u) {
	public string Key => $"catalog-{Number:00}";
	public string DisplayName => BeastmasterGameData.PetName(Number,Name);
	public string DisplayLocation {
		get {
			if(LocationType!=BeastmasterCatalogLocationType.Duty) {
				return BeastmasterGameData.ZoneName(TerritoryType,Location);
			}
			return BeastmasterGameData.DutyName(ContentFinderConditionId,Location);
		}
	}
	public BeastmasterAttribute Attribute => BeastmasterCatalog.GetAttribute(Number);
	public uint UltimateActionId => (uint)(44933+Number*2);
	public uint ReleaseActionId => UltimateActionId+1;
	public uint SummonDataId => (uint)(18915+Number);
	public BeastmasterSkillProfile SkillProfile => new BeastmasterSkillProfile(Number,DisplayName,SummonDataId,Attribute,UltimateActionId,ReleaseActionId);
}
