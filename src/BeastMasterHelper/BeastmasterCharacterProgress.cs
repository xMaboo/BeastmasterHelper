using System;
using System.Collections.Generic;
namespace Beastmaster;

[Serializable]
public sealed class BeastmasterCharacterProgress {
	public HashSet<string> CompletedObjectives { get; set; }=new HashSet<string>(StringComparer.Ordinal);
}
