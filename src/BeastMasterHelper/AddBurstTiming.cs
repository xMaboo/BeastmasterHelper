using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
namespace Beastmaster;

public static class AddBurstTiming {
	public sealed record AddBurst(string Boss,string Add,double HoldFrom,double GiveUpAt,string Label,double SpawnsAt,bool PlanInternally=true);
	public static readonly AddBurst[] Table=new AddBurst[2] {
		new AddBurst("Treant Piece","Sapling Piece",0.0,0.0,"the Treant's Sapling",0.0,PlanInternally: false),
		new AddBurst("Strix Piece","Strix Plume",68.0,105.0,"the Strix Plume",80.0)
	};
	private static AddBurst? current;
	private static DateTime fightStartedAt=DateTime.MinValue;
	private static bool addSeen;
	public static AddBurst? Current => current;
	public static IBattleChara? Target { get; private set; }
	public static bool Holding { get; private set; }
	public static bool Burst {
		get {
			if(Target!=null) {
				return current?.PlanInternally ?? false;
			}
			return false;
		}
	}
	public static double FightSeconds {
		get {
			if(!(fightStartedAt==DateTime.MinValue)) {
				return (DateTime.UtcNow-fightStartedAt).TotalSeconds;
			}
			return -1.0;
		}
	}
	public static string HoldReason {
		get {
			if(!(current==null)) {
				return $"{current.Label} ({FightSeconds:0}s in, it spawns at ~{current.SpawnsAt:0}s)";
			}
			return "";
		}
	}
	public static void Update() {
		AddBurst addBurst=null;
		IBattleChara battleChara=null;
		if(DalamudApi.Condition[ConditionFlag.BoundByDuty]&&DalamudApi.Condition[ConditionFlag.InCombat]) {
			foreach(IGameObject item in DalamudApi.ObjectTable) {
				if(!(item is IBattleNpc { IsDead: false,CurrentHp: not 0u } battleNpc)) {
					continue;
				}
				string textValue=battleNpc.Name.TextValue;
				AddBurst[] table=Table;
				foreach(AddBurst addBurst2 in table) {
					if(textValue==addBurst2.Boss) {
						addBurst=addBurst2;
					}
				}
			}
			if(addBurst!=null) {
				foreach(IGameObject item2 in DalamudApi.ObjectTable) {
					if(item2 is IBattleNpc { IsDead: false,CurrentHp: >0u,IsTargetable: not false } battleNpc2&&battleNpc2.Name.TextValue==addBurst.Add) {
						battleChara=battleNpc2;
						break;
					}
				}
			}
		}
		if(addBurst==null||(object)addBurst!=current) {
			current=addBurst;
			fightStartedAt=((addBurst==null)?DateTime.MinValue:DateTime.UtcNow);
			addSeen=false;
			Target=null;
			Holding=false;
			if(addBurst==null) {
				return;
			}
		}
		if(battleChara!=null&&!addSeen) {
			addSeen=true;
			DalamudApi.Log.Information("{Boss}: {Add} up {Seconds:0.0}s into the fight - {Planner}.",addBurst.Boss,addBurst.Add,FightSeconds,addBurst.PlanInternally?"internal burst active":"target priority active; Reaction Helper owns burst timing");
		}
		Target=battleChara;
		double fightSeconds=FightSeconds;
		Holding=addBurst.PlanInternally&&battleChara==null&&!addSeen&&fightSeconds>=addBurst.HoldFrom&&fightSeconds<addBurst.GiveUpAt;
	}
}
