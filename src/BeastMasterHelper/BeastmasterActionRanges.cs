using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;
namespace Beastmaster;

public static class BeastmasterActionRanges {
	public const float Melee=3f;
	private static readonly Dictionary<uint,float> Cache=new Dictionary<uint,float>();
	private static readonly Lock Gate=new Lock();
	public static float Of(uint actionId) {
		using(Gate.EnterScope()) {
			if(Cache.TryGetValue(actionId,out var value)) {
				return value;
			}
		}
		float num=Read(actionId);
		using(Gate.EnterScope()) {
			Cache[actionId]=num;
			return num;
		}
	}
	private static float Read(uint actionId) {
		try {
			if(DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId,out var row)) {
				return (row.Range<0)?3f:((float)row.Range);
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read the range of action {Action}.",actionId);
		}
		return 3f;
	}
	public static bool IsSelfCast(uint actionId) {
		return Of(actionId)<=0f;
	}
	public static bool InRange(uint actionId,float edgeDistance) {
		if(!IsSelfCast(actionId)) {
			return edgeDistance<=Of(actionId);
		}
		return true;
	}
	public static float EdgeDistance(IGameObject player,IGameObject target) {
		return Math.Max(0f,Vector3.Distance(player.Position,target.Position)-target.HitboxRadius-player.HitboxRadius);
	}
	public static void ClearCache() {
		using(Gate.EnterScope()) {
			Cache.Clear();
		}
	}
}
