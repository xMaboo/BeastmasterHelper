using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using InteropGenerator.Runtime;
namespace Beastmaster;

public static class BeastmasterTeam {
	public const int SlotCount=3;
	public unsafe static byte RawSlot(int slot) {
		if((slot<0||slot>=3)?true:false) {
			return 0;
		}
		try {
			ActionManager* ptr=ActionManager.Instance();
			if(ptr==null) {
				return 0;
			}
			Span<byte> beastmasterPets=ptr->BeastmasterPets;
			return (byte)((slot<beastmasterPets.Length)?beastmasterPets[slot]:0);
		}
		catch {
			return 0;
		}
	}
	public static BeastmasterCatalogEntry? SlotEntry(int slot) {
		byte b=RawSlot(slot);
		if(b==0) {
			return null;
		}
		foreach(BeastmasterCatalogEntry entry in BeastmasterCatalog.Entries) {
			if(entry.Number==b) {
				return entry;
			}
		}
		return BeastmasterGameData.EntryForPetRow(b);
	}
	public static string SlotLabel(int slot) {
		byte b=RawSlot(slot);
		if(b==0) {
			return "empty";
		}
		return SlotEntry(slot)?.DisplayName ?? $"unknown (#{b})";
	}
	public unsafe static bool? IsUnlocked(int catalogueNumber) {
		if(catalogueNumber<=0) {
			return null;
		}
		try {
			XBMManager* ptr=XBMManager.Instance();
			if(ptr==null) {
				return null;
			}
			BitArray unlockedPetsBitArray=ptr->UnlockedPetsBitArray;
			int numUnlockedPets=ptr->NumUnlockedPets;
			if(numUnlockedPets<=0||unlockedPetsBitArray.PopCount!=numUnlockedPets) {
				return null;
			}
			int num=((unlockedPetsBitArray.BitCount>0&&unlockedPetsBitArray[0])?1:0);
			int num2=catalogueNumber-num;
			if(num2<0||num2>=unlockedPetsBitArray.BitCount) {
				return null;
			}
			return unlockedPetsBitArray[num2];
		}
		catch {
			return null;
		}
	}
	public unsafe static int UnlockedCount() {
		try {
			XBMManager* ptr=XBMManager.Instance();
			return (ptr==null)?(-1):ptr->NumUnlockedPets;
		}
		catch {
			return -1;
		}
	}
}
