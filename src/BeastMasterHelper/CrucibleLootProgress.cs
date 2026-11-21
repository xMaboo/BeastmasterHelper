using System.Collections.Generic;
namespace Beastmaster;

internal sealed class CrucibleLootProgress {
	private readonly HashSet<string> attempted=new HashSet<string>();
	public bool TryBegin(string offerState) {
		return attempted.Add(offerState);
	}
	public bool HasAttempted(string offerState) {
		return attempted.Contains(offerState);
	}
	public void Reset() {
		attempted.Clear();
	}
}
