using System.Collections.Generic;
namespace Beastmaster;

public sealed record CrucibleGuideBoard(int Board,string Name,int[] EntryPool,IReadOnlyList<CrucibleGuideFight> Fights);
