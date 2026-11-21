using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Statuses;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
namespace Beastmaster;

public static class BeastmasterDutyActions {
	public const uint Challenge=46750u;
	public const uint Snarl=46751u;
	public static readonly uint[] Known=new uint[2] { 46751u, 46750u };
	private static IReadOnlySet<uint>? coveredStatuses;
	public static IReadOnlySet<uint> CoveredStatuses => coveredStatuses ?? (coveredStatuses=LoadCovered());
	public static bool Available => Offered().Count>0;
	private static IReadOnlySet<uint> LoadCovered() {
		HashSet<uint> hashSet=new HashSet<uint>();
		try {
			foreach(Lumina.Excel.Sheets.Status item in DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>()) {
				if(item.Name.ExtractText().Equals("Covered",StringComparison.OrdinalIgnoreCase)) {
					hashSet.Add(item.RowId);
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not resolve the Covered status.");
		}
		return hashSet;
	}
	public static bool IsCovered() {
		foreach(uint coveredStatus in CoveredStatuses) {
			if(BeastmasterStatusReader.Has(coveredStatus)) {
				return true;
			}
		}
		return false;
	}
	public static bool DropCovered() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return false;
		}
		bool result=false;
		foreach(IStatus status in localPlayer.StatusList) {
			if(CoveredStatuses.Contains(status.StatusId)) {
				StatusManager.ExecuteStatusOff(status.StatusId,status.SourceId);
				result=true;
			}
		}
		return result;
	}
	public unsafe static List<uint> Offered() {
		List<uint> list=new List<uint>();
		try {
			DutyActionManager* instanceIfReady=DutyActionManager.GetInstanceIfReady();
			if(instanceIfReady==null) {
				return list;
			}
			Span<uint> actionId=instanceIfReady->ActionId;
			for(int i=0;i<actionId.Length;i++) {
				if(actionId[i]!=0) {
					list.Add(actionId[i]);
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read the duty action slots.");
		}
		return list;
	}
	public unsafe static DutyActionState Read(uint actionId) {
		try {
			DutyActionManager* instanceIfReady=DutyActionManager.GetInstanceIfReady();
			if(instanceIfReady==null) {
				return DutyActionState.Absent;
			}
			Span<uint> actionId2=instanceIfReady->ActionId;
			for(int i=0;i<actionId2.Length;i++) {
				if(actionId2[i]!=actionId) {
					continue;
				}
				byte b=(byte)((i<instanceIfReady->CurCharges.Length)?instanceIfReady->CurCharges[i]:0);
				byte b2=(byte)((i<instanceIfReady->MaxCharges.Length)?instanceIfReady->MaxCharges[i]:0);
				float num=0f;
				float cooldownTotal=0f;
				if(i<instanceIfReady->Recast.Length) {
					RecastDetail recastDetail=instanceIfReady->Recast[i];
					if(recastDetail.IsActive) {
						cooldownTotal=recastDetail.Total;
						num=Math.Max(0f,recastDetail.Total-recastDetail.Elapsed);
					}
				}
				bool usable=((b2>0)?(b>0):(num<=0.05f));
				return new DutyActionState(Present: true,usable,num,cooldownTotal,b,b2);
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read duty action {Id}.",actionId);
		}
		return DutyActionState.Absent;
	}
	public static bool IsDutyAction(uint actionId) {
		if(actionId!=46751) {
			return actionId==46750;
		}
		return true;
	}
	public static bool CanUse(uint actionId) {
		return Read(actionId).Usable;
	}
	public static bool IsAvailable(uint actionId) {
		return Read(actionId).Present;
	}
	public unsafe static int SlotOf(uint actionId) {
		try {
			DutyActionManager* instanceIfReady=DutyActionManager.GetInstanceIfReady();
			if(instanceIfReady==null) {
				return -1;
			}
			Span<uint> actionId2=instanceIfReady->ActionId;
			for(int i=0;i<actionId2.Length;i++) {
				if(actionId2[i]==actionId) {
					return i;
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not find the duty action slot for {Id}.",actionId);
		}
		return -1;
	}
	public unsafe static bool Execute(uint actionId) {
		int num=SlotOf(actionId);
		if(num<0) {
			return false;
		}
		try {
			RaptureHotbarModule* ptr=RaptureHotbarModule.Instance();
			if(ptr==null) {
				return false;
			}
			return ptr->ExecuteDutyActionSlot((uint)num);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not execute duty action {Id} in slot {Slot}.",actionId,num);
			return false;
		}
	}
}
