using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
namespace Beastmaster;

public static class RotationToggleOverrides {
	private static readonly ToggleLeaseBook book=new ToggleLeaseBook();
	private static uint territory;
	private static ulong actor;
	public static readonly HashSet<string> Supported=new HashSet<string> {
		"Cooldowns", "Rally", "RallyingCheer", "PowerActions", "Burn", "AutoAttack", "OneWithNature", "Borrow", "TemperedRelease", "PartingBlow",
		"Cycle", "GapClose", "ChargeAoe", "AiAssist", "Aoe", "Opener", "TrickWaitsTp", "BeastMode", "WaveInMelee", "CloudSkim",
		"AutoSummon", "KeepCovered", "NeedVantage", "SpendPower", "Capture", "Trick"
	};
	public static int Familiar;
	public static string Summary {
		get {
			ValidContext();
			return book.Summary;
		}
	}
	public static bool ValidContext() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(territory!=DalamudApi.ClientState.TerritoryType||actor!=(localPlayer?.GameObjectId ?? 0)) {
			book.Clear();
		}
		territory=DalamudApi.ClientState.TerritoryType;
		actor=localPlayer?.GameObjectId ?? 0;
		int num;
		if(localPlayer!=null&&!localPlayer.IsDead) {
			num=((localPlayer.ClassJob.RowId==43)?1:0);
			if(num!=0) {
				goto IL_0088;
			}
		}
		else {
			num=0;
		}
		book.Clear();
		goto IL_0088;
		IL_0088:
		return (byte)num!=0;
	}
	public static bool Renew(string owner,string key,bool value,float seconds) {
		if(ValidContext()&&Supported.Contains(key)) {
			return book.Renew(owner,key,value,seconds);
		}
		return false;
	}
	public static bool Release(string owner) {
		return book.Release(owner);
	}
	public static bool Get(string key,bool baseline) {
		ValidContext();
		return book.Get(key,baseline);
	}
	public static bool IsHeld(string key) {
		ValidContext();
		return book.Override(key).HasValue;
	}
	public static OneWithNatureChoice? NatureChoice(BeastmasterConfiguration config,int summonedNumber=0) {
		ValidContext();
		bool? borrow=book.Override("Borrow");
		bool? tempered=book.Override("TemperedRelease");
		bool? flag=book.Override("OneWithNature");
		OneWithNatureChoice? result=NatureTogglePolicy.Resolve(config.RotationUseOneWithNature,config.OneWithNature,flag,borrow,tempered);
		if(flag==false||borrow.HasValue||tempered.HasValue) {
			return result;
		}
		if(!(flag ?? config.RotationUseOneWithNature)) {
			return null;
		}
		return result;
	}
	public static bool AllowsAction(BeastmasterConfiguration config,uint action) {
		switch(action) {
		case 44905u:
		return Get("Rally",config.RotationUseRally);
		case 44904u:
		return Get("RallyingCheer",config.RotationUseRallyingCheer);
		case 44895u:
		return NatureChoice(config,Familiar)==OneWithNatureChoice.Borrow;
		case 44890u:
		return NatureChoice(config,Familiar)==OneWithNatureChoice.TemperedRelease;
		case 44891u:
		return Get("PartingBlow",baseline: true);
		case 44884u:
		case 44887u:
		case 44888u:
		case 44889u:
		case 47093u:
		return Get("PowerActions",config.RotationUsePowerActions);
		default:
		return true;
		}
	}
	public static void Clear() {
		book.Clear();
	}
}
