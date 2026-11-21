using System.Collections.Generic;
namespace Beastmaster;

public sealed record BeastmasterStage(string Key,string Name,string Summary,IReadOnlyList<BeastmasterObjective> Objectives);
