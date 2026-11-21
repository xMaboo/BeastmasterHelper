using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace Beastmaster;

public sealed class BeastmasterGaugeSnapshot {
	public const int MaximumGauge=250;
	public const int ComboGaugeRequirement=100;
	private const uint BeastmasterClassJobId=43u;
	private const int RawGaugeLength=48;
	private const int BeastPowerOffset=17;
	private const int WhistleIndexOffset=19;
	private const int BeastHeartOffset=24;
	private const int TpOffset=16;
	public bool Available { get; }
	public string Status { get; }
	public byte[] Bytes { get; }
	public nint Address { get; }
	public uint SummonDataId { get; }
	public string SummonName { get; }
	public byte Tp => GetByte(16);
	public byte BeastPower => GetByte(17);
	public byte WhistleIndex => GetByte(19);
	public byte BeastHeart => GetByte(24);
	public int BeastHeartStacks => (BeastHeart>>2)&3;
	public int BeastSoulStacks => BeastHeart&3;
	public bool StacksLookSane {
		get {
			if(Available) {
				return BeastHeart<=15;
			}
			return false;
		}
	}
	public BeastmasterCatalogEntry? SummonEntry {
		get {
			if(SummonDataId!=0) {
				return BeastmasterCatalog.Entries.FirstOrDefault((BeastmasterCatalogEntry entry) => entry.SummonDataId==SummonDataId);
			}
			return null;
		}
	}
	private BeastmasterGaugeSnapshot(bool available,string status,byte[] bytes,nint address,uint summonDataId=0u,string summonName="") {
		Available=available;
		Status=status;
		Bytes=bytes;
		Address=address;
		SummonDataId=summonDataId;
		SummonName=summonName;
	}
	public unsafe static BeastmasterGaugeSnapshot Read() {
		if(!DalamudApi.ClientState.IsLoggedIn) {
			return Unavailable("Not logged in");
		}
		if(DalamudApi.PlayerState.ClassJob.RowId!=43) {
			return Unavailable("Current job is not Beastmaster");
		}
		JobGaugeManager* ptr=JobGaugeManager.Instance();
		if(ptr==null) {
			return Unavailable("JobGaugeManager.Instance() unavailable");
		}
		byte[] array=new byte[48];
		nint num=(nint)(&ptr->CurrentGauge);
		byte* ptr2=(byte*)num;
		for(int i=0;i<array.Length;i++) {
			array[i]=ptr2[i];
		}
		var (summonDataId,summonName)=FindSummon();
		return new BeastmasterGaugeSnapshot(available: true,"Read OK",array,num,summonDataId,summonName);
	}
	public static BeastmasterGaugeSnapshot ReadRaw() {
		return Read();
	}
	public string FormatRawBytes() {
		if(Bytes.Length!=0) {
			return string.Join(' ',Bytes.Select((byte value) => value.ToString("X2")));
		}
		return "Unavailable";
	}
	public static BeastmasterGaugeSnapshot Unavailable(string status) {
		return new BeastmasterGaugeSnapshot(available: false,status,Array.Empty<byte>(),IntPtr.Zero);
	}
	private static (uint DataId,string Name) FindSummon() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return (DataId: 0u,Name: "");
		}
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(!(item is IBattleChara { ObjectKind: ObjectKind.BattleNpc } battleChara)||battleChara.OwnerId!=localPlayer.EntityId||battleChara.EntityId==localPlayer.EntityId) {
				continue;
			}
			ICharacter character=battleChara;
			if(character!=null&&character.CurrentHp!=0) {
				uint baseId=battleChara.BaseId;
				if(baseId>=18916&&baseId<=18965) {
					return (DataId: battleChara.BaseId,Name: battleChara.Name.TextValue);
				}
			}
		}
		return (DataId: 0u,Name: "");
	}
	private byte GetByte(int offset) {
		if(Bytes.Length<=offset) {
			return 0;
		}
		return Bytes[offset];
	}
}
