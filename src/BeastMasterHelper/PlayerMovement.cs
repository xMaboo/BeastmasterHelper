using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
namespace Beastmaster;

public static class PlayerMovement {
	private const float MovingEpsilonSquared=0.0004f;
	private static readonly TimeSpan Linger=TimeSpan.FromMilliseconds(300L);
	private static Vector3 lastPosition;
	private static DateTime lastMovedAt=DateTime.MinValue;
	private static bool hasPosition;
	public static bool IsMoving { get; private set; }
	public static void Update() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			Reset();
			return;
		}
		Vector3 position=localPlayer.Position;
		if(!hasPosition) {
			lastPosition=position;
			hasPosition=true;
			IsMoving=false;
			return;
		}
		if(Vector3.DistanceSquared(position,lastPosition)>0.0004f) {
			lastMovedAt=DateTime.UtcNow;
		}
		lastPosition=position;
		IsMoving=lastMovedAt!=DateTime.MinValue&&DateTime.UtcNow-lastMovedAt<Linger;
	}
	public static void Reset() {
		hasPosition=false;
		IsMoving=false;
		lastMovedAt=DateTime.MinValue;
	}
}
