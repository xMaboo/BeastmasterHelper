using System;
namespace Beastmaster;

public readonly record struct DutyActionState(bool Present,bool Usable,float CooldownRemaining,float CooldownTotal,byte Charges,byte MaxCharges) {
	public bool OnCooldown => CooldownRemaining>0.05f;
	public float CooldownFraction {
		get {
			if(!(CooldownTotal<=0f)) {
				return Math.Clamp(CooldownRemaining/CooldownTotal,0f,1f);
			}
			return 0f;
		}
	}
	public static readonly DutyActionState Absent=new DutyActionState(Present: false,Usable: false,0f,0f,0,0);
}
