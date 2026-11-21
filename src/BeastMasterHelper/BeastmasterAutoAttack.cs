using System;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
namespace Beastmaster;

public static class BeastmasterAutoAttack {
	public unsafe static bool IsAttacking {
		get {
			UIState* ptr=UIState.Instance();
			if(ptr!=null) {
				return ptr->WeaponState.AutoAttackState.IsAutoAttacking;
			}
			return false;
		}
	}
	public static string LastResult { get; private set; }=string.Empty;
	public unsafe static bool Stop() {
		UIState* ptr=UIState.Instance();
		if(ptr==null||!ptr->WeaponState.AutoAttackState.IsAutoAttacking) {
			return false;
		}
		try {
			((delegate* unmanaged<AutoAttackState*,byte,byte,byte,byte>)AutoAttackState.Addresses.SetImpl.Value)(&ptr->WeaponState.AutoAttackState,0,1,1);
			LastResult="auto-attack stopped";
			return true;
		}
		catch(Exception ex) {
			LastResult="could not stop auto-attack: "+ex.Message;
			DalamudApi.Log.Warning(ex,"Stopping auto-attack failed.");
			return false;
		}
	}
}
