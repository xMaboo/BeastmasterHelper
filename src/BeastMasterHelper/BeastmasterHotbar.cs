using System;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace Beastmaster;

public static class BeastmasterHotbar {
	public static readonly uint[] Slots=new uint[16] {
		44881u, 44892u, 44894u, 47093u, 44891u, 44890u, 44895u, 44886u, 44893u, 44905u,
		44904u, 44880u, 3u, 900001u, 46751u, 46750u
	};
	public unsafe static HotbarSlotState Read(uint actionId,int playerLevel,ulong targetId) {
		string name;
		if(actionId==900001) {
			return new HotbarSlotState(Unlocked: true,BeastmasterPotion.BestInBag(out name)!=0,0f,0f);
		}
		if(BeastmasterDutyActions.IsDutyAction(actionId)) {
			DutyActionState dutyActionState=BeastmasterDutyActions.Read(actionId);
			return new HotbarSlotState(dutyActionState.Present,dutyActionState.Usable,dutyActionState.CooldownRemaining,dutyActionState.CooldownTotal);
		}
		if(!BeastmasterActions.IsUnlocked(actionId,playerLevel)) {
			return new HotbarSlotState(Unlocked: false,Usable: false,0f,0f);
		}
		try {
			ActionManager* ptr=ActionManager.Instance();
			if(ptr==null) {
				return new HotbarSlotState(Unlocked: true,Usable: false,0f,0f);
			}
			ulong targetId2=BeastmasterRotationService.SelfOr(targetId,actionId);
			bool usable=ptr->GetActionStatus(ActionType.Action,actionId,targetId2,checkRecastActive: true,checkCastingActive: true,null)==0;
			float recastTime=ptr->GetRecastTime(ActionType.Action,actionId);
			float recastTimeElapsed=ptr->GetRecastTimeElapsed(ActionType.Action,actionId);
			float cooldownRemaining=Math.Max(0f,recastTime-recastTimeElapsed);
			return new HotbarSlotState(Unlocked: true,usable,cooldownRemaining,recastTime);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read hotbar state for action {Id}.",actionId);
			return new HotbarSlotState(Unlocked: true,Usable: false,0f,0f);
		}
	}
	public unsafe static bool Press(uint actionId,ulong targetId) {
		if(BeastmasterDutyActions.IsDutyAction(actionId)) {
			return BeastmasterDutyActions.Execute(actionId);
		}
		try {
			ActionManager* ptr=ActionManager.Instance();
			if(ptr==null) {
				return false;
			}
			return ptr->UseAction(ActionType.Action,actionId,BeastmasterRotationService.SelfOr(targetId,actionId),0u,ActionManager.UseActionMode.None,0u,null);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not use action {Id} from the hotbar.",actionId);
			return false;
		}
	}
}
