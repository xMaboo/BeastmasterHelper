using System;
namespace Beastmaster;

public static class BeastmasterActionRequest {
	private static readonly object Gate=new object();
	private static uint requestedActionId;
	private static DateTime requestedAtUtc=DateTime.MinValue;
	private static readonly TimeSpan Lifetime=TimeSpan.FromSeconds(30L);
	public static uint Pending {
		get {
			lock(Gate) {
				if(requestedActionId==0) {
					return 0u;
				}
				if(DateTime.UtcNow-requestedAtUtc>Lifetime) {
					requestedActionId=0u;
					return 0u;
				}
				return requestedActionId;
			}
		}
	}
	public static double SecondsRemaining {
		get {
			lock(Gate) {
				return (requestedActionId==0)?0.0:Math.Max(0.0,(Lifetime-(DateTime.UtcNow-requestedAtUtc)).TotalSeconds);
			}
		}
	}
	public static bool IsPending(uint actionId) {
		if(actionId!=0) {
			return Pending==actionId;
		}
		return false;
	}
	public static void Toggle(uint actionId) {
		lock(Gate) {
			if(requestedActionId==actionId) {
				requestedActionId=0u;
				return;
			}
			requestedActionId=actionId;
			requestedAtUtc=DateTime.UtcNow;
		}
	}
	public static void NoteUsed(uint actionId) {
		lock(Gate) {
			if(requestedActionId==actionId) {
				requestedActionId=0u;
			}
		}
	}
	public static void Clear() {
		lock(Gate) {
			requestedActionId=0u;
		}
	}
}
