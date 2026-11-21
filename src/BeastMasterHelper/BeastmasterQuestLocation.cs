using System.Numerics;
namespace Beastmaster;

public sealed record BeastmasterQuestLocation(uint TerritoryType,uint MapRowId,Vector3 Position,string Zone,string NpcName,bool GroundOnly=false);
