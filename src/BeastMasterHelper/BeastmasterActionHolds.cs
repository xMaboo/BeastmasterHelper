using System;
using System.Collections.Generic;
namespace Beastmaster;

public static class BeastmasterActionHolds {
	private static readonly Dictionary<uint,DateTime> Until=new Dictionary<uint,DateTime>();
	public static void Hold(uint actionId,float seconds) {
		if(actionId!=0) {
			Until[actionId]=DateTime.UtcNow.AddSeconds(Math.Max(0f,seconds));
		}
	}
	public static bool IsHeld(uint actionId) {
		if(!Until.TryGetValue(actionId,out var value)) {
			return false;
		}
		if(DateTime.UtcNow<value) {
			return true;
		}
		Until.Remove(actionId);
		return false;
	}
}
