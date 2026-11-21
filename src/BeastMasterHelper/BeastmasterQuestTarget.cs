using System.Numerics;
namespace Beastmaster;

public sealed record BeastmasterQuestTarget(uint QuestRowId,byte Sequence,uint TerritoryType,uint MapRowId,Vector3 Position,string Zone,string Name);
