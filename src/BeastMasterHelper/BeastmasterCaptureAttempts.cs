using System;
using System.Collections.Generic;
using System.Linq;
namespace Beastmaster;

public static class BeastmasterCaptureAttempts {
	private static readonly TimeSpan Expiry=TimeSpan.FromMinutes(15L);
	private const int MaxEntries=512;
	private static readonly Dictionary<ulong,DateTime> Attempts=new Dictionary<ulong,DateTime>();
	private static ushort lastTerritory;
	public static int Count => Attempts.Count;
	public static bool AlreadyTried(ulong objectId) {
		if(objectId==0L) {
			return false;
		}
		if(!Attempts.TryGetValue(objectId,out var value)) {
			return false;
		}
		if(DateTime.UtcNow-value<=Expiry) {
			return true;
		}
		Attempts.Remove(objectId);
		return false;
	}
	public static void Record(ulong objectId) {
		if(objectId!=0L) {
			if(Attempts.Count>=512) {
				Prune();
			}
			Attempts[objectId]=DateTime.UtcNow;
		}
	}
	public static void NoteTerritory(ushort territory) {
		if(territory!=lastTerritory) {
			lastTerritory=territory;
			Attempts.Clear();
		}
	}
	public static void Clear() {
		Attempts.Clear();
	}
	private static void Prune() {
		DateTime cutoff=DateTime.UtcNow-Expiry;
		foreach(ulong item in (from pair in Attempts
			where pair.Value<cutoff
			select pair.Key).ToList()) {
			Attempts.Remove(item);
		}
		if(Attempts.Count<512) {
			return;
		}
		foreach(ulong item2 in (from pair in Attempts.OrderBy((KeyValuePair<ulong,DateTime> pair) => pair.Value).Take(Attempts.Count-512+1)
			select pair.Key).ToList()) {
			Attempts.Remove(item2);
		}
	}
}
