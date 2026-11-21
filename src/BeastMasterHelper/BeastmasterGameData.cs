using System;
using System.Collections.Generic;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
namespace Beastmaster;

public static class BeastmasterGameData {
	private const string XbmPetSheetName="XBMPet";
	private const string PetSheetName="Pet";
	private static readonly Dictionary<int,string> PetNameCache=new Dictionary<int,string>();
	private static readonly Dictionary<uint,string> ZoneNameCache=new Dictionary<uint,string>();
	private static readonly Dictionary<uint,string> DutyNameCache=new Dictionary<uint,string>();
	private static readonly Dictionary<uint,string> ActionNameCache=new Dictionary<uint,string>();
	private static readonly Dictionary<uint,uint> ActionIconCache=new Dictionary<uint,uint>();
	private static readonly Dictionary<uint,string> ItemNameCache=new Dictionary<uint,string>();
	private static readonly Dictionary<uint,string> TextCommandCache=new Dictionary<uint,string>();
	private static readonly Dictionary<uint,string> LogMessageCache=new Dictionary<uint,string>();
	private static readonly Dictionary<uint,string> QuestNameCache=new Dictionary<uint,string>();
	private static Dictionary<uint,int>? petRowToCatalogue;
	public static string PetName(int catalogueNumber,string fallback) {
		if(PetNameCache.TryGetValue(catalogueNumber,out string value)) {
			return value;
		}
		string text=ResolvePetName(catalogueNumber) ?? fallback;
		PetNameCache[catalogueNumber]=text;
		return text;
	}
	private static string? ResolvePetName(int catalogueNumber) {
		try {
			if(!DalamudApi.DataManager.Excel.GetSheet<RawRow>(null,"XBMPet").TryGetRow((uint)catalogueNumber,out var row)) {
				return null;
			}
			uint rowId=Convert.ToUInt32(row.ReadColumn(0));
			if(!DalamudApi.DataManager.Excel.GetSheet<RawRow>(null,"Pet").TryGetRow(rowId,out var row2)) {
				return null;
			}
			if(!(row2.ReadColumn(0) is ReadOnlySeString readOnlySeString)) {
				return null;
			}
			string value=readOnlySeString.ExtractText();
			return string.IsNullOrWhiteSpace(value)?null:Capitalise(value);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not resolve pet name for catalogue entry {Number}.",catalogueNumber);
			return null;
		}
	}
	public static string ZoneName(uint territoryType,string fallback="") {
		if(territoryType==0) {
			return fallback;
		}
		if(ZoneNameCache.TryGetValue(territoryType,out string value)) {
			return value;
		}
		string text=fallback;
		try {
			if(DalamudApi.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryType,out var row)) {
				string text2=row.PlaceName.Value.Name.ExtractText();
				if(!string.IsNullOrWhiteSpace(text2)) {
					text=text2;
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not resolve zone name for territory {Territory}.",territoryType);
		}
		ZoneNameCache[territoryType]=text;
		return text;
	}
	public static string DutyName(uint contentFinderConditionId,string fallback="") {
		if(contentFinderConditionId==0) {
			return fallback;
		}
		if(DutyNameCache.TryGetValue(contentFinderConditionId,out string value)) {
			return value;
		}
		string text=fallback;
		try {
			if(DalamudApi.DataManager.GetExcelSheet<ContentFinderCondition>().TryGetRow(contentFinderConditionId,out var row)) {
				string value2=row.Name.ExtractText();
				if(!string.IsNullOrWhiteSpace(value2)) {
					text=Capitalise(value2);
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not resolve duty name for content {Content}.",contentFinderConditionId);
		}
		DutyNameCache[contentFinderConditionId]=text;
		return text;
	}
	public static uint ActionIcon(uint actionId) {
		switch(actionId) {
		case 0u:
		return 0u;
		case 900001u:
		return BeastmasterPotion.Icon();
		default: {
			if(ActionIconCache.TryGetValue(actionId,out var value)) {
				return value;
			}
			uint num=0u;
			try {
				if(DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId,out var row)) {
					num=row.Icon;
				}
			}
			catch(Exception exception) {
				DalamudApi.Log.Warning(exception,"Could not resolve the icon for action {Id}.",actionId);
			}
			ActionIconCache[actionId]=num;
			return num;
		}
		}
	}
	public static string ActionName(uint actionId,string fallback="") {
		switch(actionId) {
		case 0u:
		return fallback;
		case 900001u:
		return BeastmasterPotion.DisplayName();
		default: {
			if(ActionNameCache.TryGetValue(actionId,out string value)) {
				return value;
			}
			string text=fallback;
			try {
				if(DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId,out var row)) {
					string text2=row.Name.ExtractText();
					if(!string.IsNullOrWhiteSpace(text2)) {
						text=text2;
					}
				}
			}
			catch(Exception exception) {
				DalamudApi.Log.Warning(exception,"Could not resolve action name for action {Action}.",actionId);
			}
			ActionNameCache[actionId]=text;
			return text;
		}
		}
	}
	public static string ItemName(uint itemId,string fallback="") {
		if(itemId==0) {
			return fallback;
		}
		if(ItemNameCache.TryGetValue(itemId,out string value)) {
			return value;
		}
		string text=fallback;
		try {
			if(DalamudApi.DataManager.GetExcelSheet<Item>().TryGetRow(itemId,out var row)) {
				string text2=row.Name.ExtractText();
				if(!string.IsNullOrWhiteSpace(text2)) {
					text=text2;
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not resolve item name for item {Item}.",itemId);
		}
		ItemNameCache[itemId]=text;
		return text;
	}
	private static string Capitalise(string value) {
		if(!string.IsNullOrEmpty(value)&&!char.IsUpper(value[0])) {
			return char.ToUpperInvariant(value[0])+value.Substring(1);
		}
		return value;
	}
	public static string TextCommand(uint rowId,string fallback) {
		if(TextCommandCache.TryGetValue(rowId,out string value)) {
			return value;
		}
		string text=fallback;
		try {
			if(DalamudApi.DataManager.Excel.GetSheet<RawRow>(null,"TextCommand").TryGetRow(rowId,out var row)&&row.ReadColumn(0) is ReadOnlySeString readOnlySeString) {
				string text2=readOnlySeString.ExtractText();
				if(!string.IsNullOrWhiteSpace(text2)&&text2.StartsWith('/')) {
					text=text2;
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not resolve text command {Row}.",rowId);
		}
		TextCommandCache[rowId]=text;
		return text;
	}
	public static string LogMessage(uint rowId) {
		if(LogMessageCache.TryGetValue(rowId,out string value)) {
			return value;
		}
		string text=string.Empty;
		try {
			if(DalamudApi.DataManager.GetExcelSheet<LogMessage>().TryGetRow(rowId,out var row)) {
				text=row.Text.ExtractText();
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not resolve log message {Row}.",rowId);
		}
		LogMessageCache[rowId]=text;
		return text;
	}
	public static string QuestName(uint questRowId,string fallback="") {
		if(questRowId==0) {
			return fallback;
		}
		if(QuestNameCache.TryGetValue(questRowId,out string value)) {
			return value;
		}
		string text=fallback;
		try {
			if(DalamudApi.DataManager.GetExcelSheet<Quest>().TryGetRow(questRowId,out var row)) {
				string text2=row.Name.ExtractText();
				if(!string.IsNullOrWhiteSpace(text2)) {
					text=text2;
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not resolve quest name for quest {Quest}.",questRowId);
		}
		QuestNameCache[questRowId]=text;
		return text;
	}
	public static BeastmasterCatalogEntry? EntryForPetRow(uint petRowId) {
		if(petRowId==0) {
			return null;
		}
		if(petRowToCatalogue==null) {
			petRowToCatalogue=new Dictionary<uint,int>();
			try {
				ExcelSheet<RawRow> sheet=DalamudApi.DataManager.Excel.GetSheet<RawRow>(null,"XBMPet");
				foreach(BeastmasterCatalogEntry entry in BeastmasterCatalog.Entries) {
					if(sheet.TryGetRow((uint)entry.Number,out var row)) {
						petRowToCatalogue[Convert.ToUInt32(row.ReadColumn(0))]=entry.Number;
					}
				}
			}
			catch(Exception exception) {
				DalamudApi.Log.Warning(exception,"Could not build the pet-row index.");
			}
		}
		if(!petRowToCatalogue.TryGetValue(petRowId,out var value)) {
			return null;
		}
		foreach(BeastmasterCatalogEntry entry2 in BeastmasterCatalog.Entries) {
			if(entry2.Number==value) {
				return entry2;
			}
		}
		return null;
	}
	public static void ClearCache() {
		PetNameCache.Clear();
		ZoneNameCache.Clear();
		DutyNameCache.Clear();
		ActionNameCache.Clear();
		ActionIconCache.Clear();
		ItemNameCache.Clear();
		QuestNameCache.Clear();
		TextCommandCache.Clear();
		LogMessageCache.Clear();
		petRowToCatalogue=null;
	}
}
