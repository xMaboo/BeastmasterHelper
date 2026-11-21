using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
namespace Beastmaster;

public static class BeastmasterStatusReader {
	public static bool Has(uint statusId) {
		if(statusId==0) {
			return false;
		}
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return false;
		}
		foreach(IStatus status in localPlayer.StatusList) {
			if(status.StatusId==statusId) {
				return true;
			}
		}
		return false;
	}
	public static bool FamiliarHas(uint statusId) {
		if(statusId==0) {
			return false;
		}
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return false;
		}
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(!(item is IBattleNpc battleNpc)||battleNpc.OwnerId!=localPlayer.EntityId||battleNpc.EntityId==localPlayer.EntityId) {
				continue;
			}
			foreach(IStatus status in battleNpc.StatusList) {
				if(status.StatusId==statusId) {
					return true;
				}
			}
		}
		return false;
	}
	public static bool EitherHas(uint statusId) {
		if(!Has(statusId)) {
			return FamiliarHas(statusId);
		}
		return true;
	}
	public static bool EitherHasAny(IReadOnlyList<uint> statusIds) {
		foreach(uint statusId in statusIds) {
			if(EitherHas(statusId)) {
				return true;
			}
		}
		return false;
	}
	public static bool FamiliarHasAny(IReadOnlyList<uint> statusIds) {
		foreach(uint statusId in statusIds) {
			if(FamiliarHas(statusId)) {
				return true;
			}
		}
		return false;
	}
	public static bool HasAny(IReadOnlyList<uint> statusIds) {
		foreach(uint statusId in statusIds) {
			if(Has(statusId)) {
				return true;
			}
		}
		return false;
	}
	public static InstinctAffinity HeldHeart() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return InstinctAffinity.None;
		}
		foreach(IStatus status in localPlayer.StatusList) {
			InstinctAffinity instinctAffinity=BeastmasterActions.AffinityOfHeart(status.StatusId);
			if(instinctAffinity!=InstinctAffinity.None) {
				return instinctAffinity;
			}
		}
		return InstinctAffinity.None;
	}
	public static uint InfinitiveHalf() {
		if(!Has(4599u)) {
			if(!Has(4600u)) {
				return 0u;
			}
			return 4600u;
		}
		return 4599u;
	}
}
