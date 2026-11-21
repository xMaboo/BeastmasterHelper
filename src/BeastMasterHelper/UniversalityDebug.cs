using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Statuses;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace Beastmaster;

public static class UniversalityDebug {
	public static (string Verdict,bool Good,string[] Details) Summarise(BeastmasterRotationRunner runner,BeastmasterConfiguration configuration,int level) {
		BeastmasterGaugeSnapshot beastmasterGaugeSnapshot=BeastmasterGaugeSnapshot.Read();
		InstinctAffinity heldHeart=runner.HeldHeart;
		InstinctAffinity wantedAffinity=runner.WantedAffinity;
		uint heldInfinitiveHalf=runner.HeldInfinitiveHalf;
		uint num=BeastmasterActions.IntentionalHalf(wantedAffinity);
		int num2=(beastmasterGaugeSnapshot.Available?beastmasterGaugeSnapshot.Tp:0);
		int num3=(beastmasterGaugeSnapshot.Available?beastmasterGaugeSnapshot.BeastHeartStacks:0);
		int value=(beastmasterGaugeSnapshot.Available?beastmasterGaugeSnapshot.BeastPower:0);
		float num4=RecastRemaining(47093u);
		bool flag=BeastmasterActions.IsUnlocked(44905u,level);
		float num5=RecastRemaining(44905u);
		string text;
		if(flag) {
			text=((num5>0.5f)?$"on cooldown ({num5:0}s)":$"ready ({num3}/{configuration.RotationRallyMinStacks} stacks)");
		}
		else {
			text="not learned";
		}
		bool item=false;
		string item2;
		if(level<50) {
			item2=$"Universality unlocks at level 50 (you are {level}).";
		}
		else if(heldInfinitiveHalf==0) {
			item2="No Sunstrider/Moonstalker window open yet - the axe that completes a wheel step opens one; Universality is the 250-TP axe inside it.";
		}
		else if(num2>=250) {
			item2=$"READY - {HalfName(heldInfinitiveHalf)} window open ({StatusRemaining(heldInfinitiveHalf):0.0}s left), TP {num2}: the next opposite-half axe is Universality.";
			item=true;
		}
		else if(!flag) {
			item2=$"{HalfName(heldInfinitiveHalf)} window open ({StatusRemaining(heldInfinitiveHalf):0.0}s left) but TP is {num2}/250 and Rally is not learned.";
		}
		else if(num5>0.5f) {
			item2=$"{HalfName(heldInfinitiveHalf)} window open ({StatusRemaining(heldInfinitiveHalf):0.0}s left) but TP is {num2}/250 and Rally is on cooldown ({num5:0}s).";
		}
		else if(num3<configuration.RotationRallyMinStacks) {
			item2=$"{HalfName(heldInfinitiveHalf)} window open but Rally has only {num3}/{configuration.RotationRallyMinStacks} Mastered Instinct stacks (TP {num2}/250).";
		}
		else {
			item2=$"{HalfName(heldInfinitiveHalf)} window open ({StatusRemaining(heldInfinitiveHalf):0.0}s left), TP {num2}/250 - Rally is ready and should fire now, then the opposite-half axe.";
			item=true;
		}
		string[] array=new string[8] {
			$"Universality landed: {runner.UniversalitySession} this session, {configuration.UniversalityCombosLanded} total",
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		DefaultInterpolatedStringHandler defaultInterpolatedStringHandler=new DefaultInterpolatedStringHandler(24,3);
		defaultInterpolatedStringHandler.AppendLiteral("Heart: ");
		defaultInterpolatedStringHandler.AppendFormatted((heldHeart==InstinctAffinity.None)?"none":((object)heldHeart));
		defaultInterpolatedStringHandler.AppendLiteral(" -> axe ");
		defaultInterpolatedStringHandler.AppendFormatted((wantedAffinity==InstinctAffinity.None)?"-":((object)wantedAffinity));
		defaultInterpolatedStringHandler.AppendLiteral(" (fills ");
		defaultInterpolatedStringHandler.AppendFormatted((num==0)?"-":HalfName(num));
		defaultInterpolatedStringHandler.AppendLiteral(")");
		array[1]=defaultInterpolatedStringHandler.ToStringAndClear();
		array[2]="Half banked: "+((heldInfinitiveHalf==0)?"none":HalfName(heldInfinitiveHalf))+"   Window open: "+((heldInfinitiveHalf!=0)?"yes":"no");
		array[3]=$"Half time left: {((heldInfinitiveHalf==0)?"-":$"{StatusRemaining(heldInfinitiveHalf):0.0}s")}   Heart time left: {HeartRemaining():0.0}s";
		array[4]=$"TP {num2}/250   Familiar TP {value}/100   Trick: {((num4>0.5f)?$"cooldown {num4:0}s":"ready")}";
		array[5]="Rally: "+text;
		array[6]=((runner.LastRallyNote.Length==0)?"Last Rally: not pressed yet":$"Last Rally ({(DateTime.UtcNow-runner.LastRallyAt).TotalSeconds:0}s ago): {runner.LastRallyNote}");
		array[7]="Rotation: "+runner.NextActionName+" - "+runner.NextActionReason;
		string[] item3=array;
		return (Verdict: item2,Good: item,Details: item3);
	}
	private static float StatusRemaining(uint statusId) {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return 0f;
		}
		foreach(IStatus status in localPlayer.StatusList) {
			if(status.StatusId==statusId) {
				return status.RemainingTime;
			}
		}
		return 0f;
	}
	private static float HeartRemaining() {
		return BeastmasterActions.HeartStatuses.Select(StatusRemaining).DefaultIfEmpty(0f).Max();
	}
	private static string HalfName(uint half) {
		return half switch {
			4600u => "Moonstalker",
			4599u => "Sunstrider",
			_ => "?",
		};
	}
	private unsafe static float RecastRemaining(uint actionId) {
		ActionManager* ptr=ActionManager.Instance();
		if(ptr==null) {
			return 0f;
		}
		float recastTime=ptr->GetRecastTime(ActionType.Action,actionId);
		float recastTimeElapsed=ptr->GetRecastTimeElapsed(ActionType.Action,actionId);
		return Math.Max(0f,recastTime-recastTimeElapsed);
	}
}
