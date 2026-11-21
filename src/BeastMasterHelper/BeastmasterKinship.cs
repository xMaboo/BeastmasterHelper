using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Statuses;
using Lumina.Excel;
namespace Beastmaster;

public static class BeastmasterKinship {
	private const string XbmPetSheetName="XBMPet";
	private static readonly Dictionary<int,BeastKin> KinCache=new Dictionary<int,BeastKin>();
	public static readonly BeastKin[] All=new BeastKin[8] {
		BeastKin.Beastkin,
		BeastKin.Vilekin,
		BeastKin.Cloudkin,
		BeastKin.Seedkin,
		BeastKin.Wavekin,
		BeastKin.Scalekin,
		BeastKin.Soulkin,
		BeastKin.Ashkin
	};
	public static string Effect(BeastKin kin) {
		return kin switch {
			BeastKin.Beastkin => "Beastskin: -25% physical damage, +5% crit, 30s",
			BeastKin.Vilekin => "Vileskin: 100% block rate, +12 TP per block, 15s",
			BeastKin.Cloudkin => "Cloud Skim: a dash, +5% evasion for 60s",
			BeastKin.Seedkin => "Seedsower: AoE -10% damage dealt and a DoT, 60s",
			BeastKin.Wavekin => "Quelling Wave: 350p at 30y every 2.5s, +10 TP (+50 on a dispel)",
			BeastKin.Scalekin => "Scaleskin: magic barrier worth 8% HP, +85 TP when it breaks",
			BeastKin.Soulkin => "Soul Crush: interrupt, 1000p and +100 TP if it lands",
			BeastKin.Ashkin => "Scouring Ash: cleanse, +10% HP and +100 TP on a removal",
			_ => "Unknown",
		};
	}
	public static float Recast(BeastKin kin) {
		return kin switch {
			BeastKin.Wavekin => 2.5f,
			BeastKin.Cloudkin => 3f,
			BeastKin.Soulkin => 30f,
			BeastKin.Ashkin => 60f,
			_ => 90f,
		};
	}
	public static bool IsDamageGcd(BeastKin kin) {
		return kin==BeastKin.Wavekin;
	}
	public static bool IsMitigation(BeastKin kin) {
		if((uint)(kin-1)<=1u||kin==BeastKin.Scalekin) {
			return true;
		}
		return false;
	}
	public static BeastKin Borrowed() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return BeastKin.Unknown;
		}
		foreach(IStatus status in localPlayer.StatusList) {
			BeastKin beastKin=BeastmasterActions.KinshipOfStatus(status.StatusId);
			if(beastKin!=BeastKin.Unknown) {
				return beastKin;
			}
		}
		return BeastKin.Unknown;
	}
	public static float BorrowedRemaining() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return -1f;
		}
		foreach(IStatus status in localPlayer.StatusList) {
			if(BeastmasterActions.KinshipOfStatus(status.StatusId)!=BeastKin.Unknown) {
				return status.RemainingTime;
			}
		}
		return -1f;
	}
	public static BeastKin KinOf(int catalogueNumber) {
		if(catalogueNumber<=0) {
			return BeastKin.Unknown;
		}
		if(KinCache.TryGetValue(catalogueNumber,out var value)) {
			return value;
		}
		BeastKin beastKin=BeastKin.Unknown;
		try {
			if(DalamudApi.DataManager.Excel.GetSheet<RawRow>(null,"XBMPet").TryGetRow((uint)catalogueNumber,out var row)) {
				int num=Convert.ToInt32(row.ReadColumn(1));
				if(num>=1&&num<=8) {
					beastKin=(BeastKin)num;
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not resolve kinship for catalogue entry {Number}.",catalogueNumber);
		}
		KinCache[catalogueNumber]=beastKin;
		return beastKin;
	}
	public static void ClearCache() {
		KinCache.Clear();
	}
	public static List<int> DefaultPriority() {
		int num=8;
		List<int> list=new List<int>(num);
		CollectionsMarshal.SetCount(list,num);
		Span<int> span=CollectionsMarshal.AsSpan(list);
		span[0]=5;
		span[1]=6;
		span[2]=1;
		span[3]=2;
		span[4]=7;
		span[5]=4;
		span[6]=8;
		span[7]=3;
		return list;
	}
}
