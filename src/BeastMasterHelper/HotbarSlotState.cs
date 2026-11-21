using System;
namespace Beastmaster;

public readonly record struct HotbarSlotState(bool Unlocked,bool Usable,float CooldownRemaining,float CooldownTotal) {
	public float CooldownFraction {
		get {
			if(!(CooldownTotal<=0f)) {
				return Math.Clamp(CooldownRemaining/CooldownTotal,0f,1f);
			}
			return 0f;
		}
	}
	public bool OnCooldown => CooldownRemaining>0.05f;
}
