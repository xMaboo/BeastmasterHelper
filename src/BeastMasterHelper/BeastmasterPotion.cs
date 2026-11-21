using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;
namespace Beastmaster;

public static class BeastmasterPotion {
	private const string PotionName="Gemdraught of Strength";
	private const uint MedicatedStatus=49u;
	private static List<(uint Id,string Name,uint Level)>? grades;
	public static bool Medicated => BeastmasterStatusReader.Has(49u);
	public static string LastResult { get; private set; }=string.Empty;
	public static IReadOnlyList<(uint Id,string Name,uint Level)> Grades() {
		if(grades!=null) {
			return grades;
		}
		List<(uint,string,uint)> list=new List<(uint,string,uint)>();
		try {
			foreach(Item item in DalamudApi.DataManager.GetExcelSheet<Item>()) {
				string text=item.Name.ToString();
				if(text.Contains("Gemdraught of Strength",StringComparison.OrdinalIgnoreCase)) {
					list.Add((item.RowId,text,item.LevelItem.RowId));
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not list the Gemdraught of Strength grades.");
		}
		grades=(from g in list
			orderby g.Item3 descending,g.Item1 descending
			select g).ToList();
		return grades;
	}
	public static string DisplayName() {
		BestInBag(out string name);
		if(name.Length<=0) {
			return "Gemdraught of Strength";
		}
		return name;
	}
	public static uint Icon() {
		try {
			ExcelSheet<Item> excelSheet=DalamudApi.DataManager.GetExcelSheet<Item>();
			uint num=BestInBag(out string _);
			if(num>=1000000) {
				num-=1000000;
			}
			if(num==0&&Grades().Count>0) {
				num=Grades()[0].Id;
			}
			Item row;
			return (uint)((num!=0&&excelSheet.TryGetRow(num,out row))?row.Icon:0);
		}
		catch {
			return 0u;
		}
	}
	public unsafe static uint BestInBag(out string name) {
		name=string.Empty;
		InventoryManager* ptr=InventoryManager.Instance();
		if(ptr==null) {
			return 0u;
		}
		foreach(var (num,text,_) in Grades()) {
			if(ptr->GetInventoryItemCount(num,isHq: true,checkEquipped: true,checkArmory: true,0)>0) {
				name=text+" (HQ)";
				return num+1000000;
			}
			if(ptr->GetInventoryItemCount(num,isHq: false,checkEquipped: true,checkArmory: true,0)>0) {
				name=text;
				return num;
			}
		}
		return 0u;
	}
	public unsafe static bool TryUse() {
		ActionManager* ptr=ActionManager.Instance();
		uint num=BestInBag(out string name);
		if(ptr==null||num==0) {
			LastResult="no Gemdraught of Strength in the bag";
			return false;
		}
		if(ptr->GetActionStatus(ActionType.Item,num,3758096384uL,checkRecastActive: true,checkCastingActive: true,null)!=0) {
			LastResult=name+" is not ready";
			return false;
		}
		bool flag=ptr->UseAction(ActionType.Item,num,3758096384uL,65535u,ActionManager.UseActionMode.None,0u,null);
		LastResult=(flag?("drank "+name):("the game refused "+name));
		if(flag) {
			DalamudApi.Log.Information("Potion: {Result}.",LastResult);
		}
		return flag;
	}
}
