using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
namespace Beastmaster;

public static class BeastmasterPetCommands {
	public const string Away="Away";
	public const string Heel="Heel";
	public const string Place="Place";
	public const string Stay="Stay";
	public const string Guard="Guard";
	public const string Steady="Steady";
	private static readonly Dictionary<string,uint> Ids=new Dictionary<string,uint>();
	private static uint IdOf(string name) {
		if(Ids.TryGetValue(name,out var value)) {
			return value;
		}
		uint num=0u;
		try {
			foreach(PetAction item in DalamudApi.DataManager.GetExcelSheet<PetAction>()) {
				if(item.Name.ExtractText().Equals(name,StringComparison.OrdinalIgnoreCase)) {
					num=item.RowId;
					break;
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not resolve the {Name} pet action.",name);
		}
		Ids[name]=num;
		DalamudApi.Log.Information("Pet action {Name}: id {Id}.",name,num);
		return num;
	}
	public unsafe static bool Send(string name) {
		uint num=IdOf(name);
		ActionManager* ptr=ActionManager.Instance();
		if(num!=0&&ptr!=null) {
			return ptr->UseAction(ActionType.PetAction,num,3758096384uL,0u,ActionManager.UseActionMode.None,0u,null);
		}
		return false;
	}
}
