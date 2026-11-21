using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
namespace Beastmaster;

internal static class CrucibleEntryRules {
	public const uint LaudaTerritory=148u;
	public static readonly Vector3 LaudaPosition=new Vector3(25.25f,-6f,64.85f);
	public const float InteractionYalms=5.5f;
	public static bool AtLauda() {
		if(DalamudApi.ClientState.TerritoryType!=148) {
			return false;
		}
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer!=null) {
			return InInteractionRange(localPlayer.Position,LaudaPosition);
		}
		return false;
	}
	public static bool InInteractionRange(Vector3 player,Vector3 npc) {
		if(Vector2.Distance(new Vector2(player.X,player.Z),new Vector2(npc.X,npc.Z))<=5.5f) {
			return MathF.Abs(player.Y-npc.Y)<=3f;
		}
		return false;
	}
}
