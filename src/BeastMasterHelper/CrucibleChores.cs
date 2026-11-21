using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Statuses;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using Lumina.Excel.Sheets;
namespace Beastmaster;

internal static class CrucibleChores {
	private const uint WellFedStatus=48u;
	private const uint MealCategory=46u;
	private static List<CrucibleFood> foodCache=new List<CrucibleFood>();
	private static DateTime foodCachedAt=DateTime.MinValue;
	public static string LastResult { get; private set; }="not attempted";
	public unsafe static bool RepairWindowOpen => RepairWindow()!=null;
	public unsafe static int LowestConditionPercent() {
		InventoryManager* ptr=InventoryManager.Instance();
		if(ptr==null) {
			return -1;
		}
		InventoryContainer* inventoryContainer=ptr->GetInventoryContainer(InventoryType.EquippedItems);
		if(inventoryContainer==null) {
			return -1;
		}
		ushort num=ushort.MaxValue;
		for(int i=0;i<inventoryContainer->Size;i++) {
			InventoryItem* inventorySlot=inventoryContainer->GetInventorySlot(i);
			if(inventorySlot!=null&&inventorySlot->ItemId!=0&&inventorySlot->Condition<num) {
				num=inventorySlot->Condition;
			}
		}
		if(num!=ushort.MaxValue) {
			return (int)Math.Ceiling((double)(int)num/300.0);
		}
		return -1;
	}
	private unsafe static AddonRepair* RepairWindow() {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("Repair").Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			return null;
		}
		return (AddonRepair*)address;
	}
	public unsafe static bool OpenRepair() {
		ActionManager* ptr=ActionManager.Instance();
		if(ptr==null) {
			LastResult="ActionManager unavailable";
			return false;
		}
		bool flag=ptr->UseAction(ActionType.GeneralAction,6u,3758096384uL,0u,ActionManager.UseActionMode.None,0u,null);
		LastResult=(flag?"asked the game to open Repair":"the Repair action was refused");
		return flag;
	}
	public unsafe static bool RepairAll() {
		AddonRepair* ptr=RepairWindow();
		if(ptr==null) {
			LastResult="the Repair window is not open";
			return false;
		}
		if(ptr->RepairAllButton==null||!ptr->RepairAllButton->IsEnabled) {
			LastResult="Repair All is disabled (no Dark Matter, or nothing repairable)";
			return false;
		}
		AtkValue* ptr2=stackalloc AtkValue[1];
		ptr2->Type=AtkValueType.Int;
		ptr2->Int=0;
		((AtkUnitBase*)ptr)->FireCallback(1u,ptr2,close: true);
		LastResult="pressed Repair All";
		return true;
	}
	public unsafe static void CloseRepair() {
		AddonRepair* ptr=RepairWindow();
		if(ptr!=null) {
			AtkValue* ptr2=stackalloc AtkValue[1];
			ptr2->Type=AtkValueType.Int;
			ptr2->Int=-1;
			((AtkUnitBase*)ptr)->FireCallback(1u,ptr2,close: true);
		}
	}
	public unsafe static IReadOnlyList<CrucibleFood> Foods() {
		if((DateTime.UtcNow-foodCachedAt).TotalSeconds<2.0) {
			return foodCache;
		}
		foodCachedAt=DateTime.UtcNow;
		try {
			InventoryManager* ptr=InventoryManager.Instance();
			ExcelSheet<Item> excelSheet=DalamudApi.DataManager.GetExcelSheet<Item>();
			if(ptr==null||excelSheet==null) {
				return foodCache;
			}
			Dictionary<uint,int> dictionary=new Dictionary<uint,int>();
			InventoryType[] array=new InventoryType[4] {
				InventoryType.Inventory1,
				InventoryType.Inventory2,
				InventoryType.Inventory3,
				InventoryType.Inventory4
			};
			int i;
			for(i=0;i<array.Length;i++) {
				InventoryType inventoryType=array[i];
				InventoryContainer* inventoryContainer=ptr->GetInventoryContainer(inventoryType);
				if(inventoryContainer==null) {
					continue;
				}
				for(int j=0;j<inventoryContainer->Size;j++) {
					InventoryItem* inventorySlot=inventoryContainer->GetInventorySlot(j);
					if(inventorySlot!=null&&inventorySlot->ItemId!=0) {
						dictionary[inventorySlot->ItemId]=dictionary.GetValueOrDefault(inventorySlot->ItemId)+inventorySlot->Quantity;
					}
				}
			}
			List<CrucibleFood> list=new List<CrucibleFood>();
			foreach(KeyValuePair<uint,int> item in dictionary) {
				item.Deconstruct(out var key,out i);
				uint num=key;
				int count=i;
				if(IsFood(excelSheet,num,out string name)) {
					list.Add(new CrucibleFood(num,name,count));
				}
			}
			foodCache=list.OrderBy((CrucibleFood f) => f.Name).ToList();
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not list the food in the bag.");
		}
		return foodCache;
	}
	private static bool IsFood(ExcelSheet<Item> items,uint id,out string name) {
		name=string.Empty;
		if(!items.TryGetRow(id,out var row)||row.ItemUICategory.RowId!=46||!row.ItemAction.IsValid||row.ItemAction.Value.Data[0]!=48) {
			return false;
		}
		name=row.Name.ToString();
		return name.Length>0;
	}
	public static float FoodMinutesLeft() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return 0f;
		}
		foreach(IStatus status in localPlayer.StatusList) {
			if(status.StatusId==48) {
				return Math.Max(0f,status.RemainingTime)/60f;
			}
		}
		return 0f;
	}
	public unsafe static bool HasFood(uint itemId) {
		InventoryManager* ptr=InventoryManager.Instance();
		if(ptr!=null) {
			if(ptr->GetInventoryItemCount(itemId,isHq: true,checkEquipped: true,checkArmory: true,0)<=0) {
				return ptr->GetInventoryItemCount(itemId,isHq: false,checkEquipped: true,checkArmory: true,0)>0;
			}
			return true;
		}
		return false;
	}
	public unsafe static bool UseFood(uint itemId) {
		InventoryManager* ptr=InventoryManager.Instance();
		ActionManager* ptr2=ActionManager.Instance();
		if(ptr==null||ptr2==null) {
			LastResult="inventory or actions unavailable";
			return false;
		}
		uint num=((ptr->GetInventoryItemCount(itemId,isHq: true,checkEquipped: true,checkArmory: true,0)>0)?(itemId+1000000):itemId);
		if(num==itemId&&ptr->GetInventoryItemCount(itemId,isHq: false,checkEquipped: true,checkArmory: true,0)==0) {
			LastResult="none of that food is left";
			return false;
		}
		bool flag=ptr2->UseAction(ActionType.Item,num,3758096384uL,65535u,ActionManager.UseActionMode.None,0u,null);
		LastResult=(flag?"ate the food":"the game refused to use the food");
		return flag;
	}
}
