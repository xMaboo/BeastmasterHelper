using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;
namespace Beastmaster;

public sealed class BeastmasterDebugDataService {
	private const int ResultLimit=200;
	public string GetCharacter() {
		ulong contentId=DalamudApi.PlayerState.ContentId;
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(!DalamudApi.ClientState.IsLoggedIn||localPlayer==null) {
			return "Not logged in, so character data cannot be read.";
		}
		string text=localPlayer.HomeWorld.Value.Name.ExtractText();
		return JoinLines("Type: current character","ContentId: "+contentId.ToString(CultureInfo.InvariantCulture),"Name: "+localPlayer.Name.TextValue,"World: "+text);
	}
	public string GetLocation() {
		uint territoryType=DalamudApi.ClientState.TerritoryType;
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		Vector3 vector=localPlayer?.Position ?? Vector3.Zero;
		string text=string.Empty;
		uint value=0u;
		if(DalamudApi.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryType,out var row)) {
			text=row.PlaceName.Value.Name.ExtractText();
			value=row.Map.RowId;
		}
		return JoinLines("Type: current position",$"TerritoryType: {territoryType}","Zone: "+text,$"Map.RowId: {value}",(localPlayer==null)?"Position: no local character":$"World position: X={vector.X:0.###}, Y={vector.Y:0.###}, Z={vector.Z:0.###}");
	}
	public string FindClassJobs(string query) {
		return FormatMatches("Job ClassJob",query,from row in DalamudApi.DataManager.GetExcelSheet<ClassJob>()
			select (RowId: row.RowId,Name: row.Name.ExtractText()));
	}
	public string FindQuests(string query) {
		query=query.Trim();
		if(query.Length==0) {
			return "Quest\nEnter a name keyword before reading.";
		}
		Quest[] array=(from quest in DalamudApi.DataManager.GetExcelSheet<Quest>()
			where quest.RowId!=0&&quest.Name.ExtractText().Contains(query,StringComparison.OrdinalIgnoreCase)
			orderby quest.RowId
			select quest).Take(201).ToArray();
		StringBuilder stringBuilder=new StringBuilder().AppendLine("Type: Quest");
		StringBuilder stringBuilder2=stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(9,1,stringBuilder);
		handler.AppendLiteral("Keyword: ");
		handler.AppendFormatted(query);
		StringBuilder stringBuilder3=stringBuilder2.AppendLine(ref handler);
		foreach(Quest item in array.Take(200)) {
			string value=item.Name.ExtractText();
			stringBuilder=stringBuilder3;
			StringBuilder stringBuilder4=stringBuilder;
			handler=new StringBuilder.AppendInterpolatedStringHandler(9,2,stringBuilder);
			handler.AppendLiteral("RowId=");
			handler.AppendFormatted(item.RowId);
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(value);
			stringBuilder4.AppendLine(ref handler);
			stringBuilder=stringBuilder3;
			StringBuilder stringBuilder5=stringBuilder;
			handler=new StringBuilder.AppendInterpolatedStringHandler(32,2,stringBuilder);
			handler.AppendLiteral("  IssuerStart=");
			handler.AppendFormatted(item.IssuerStart.RowId);
			handler.AppendLiteral(" | IssuerLocation=");
			handler.AppendFormatted(item.IssuerLocation.RowId);
			stringBuilder5.AppendLine(ref handler);
			stringBuilder=stringBuilder3;
			StringBuilder stringBuilder6=stringBuilder;
			handler=new StringBuilder.AppendInterpolatedStringHandler(35,2,stringBuilder);
			handler.AppendLiteral("  JournalGenre=");
			handler.AppendFormatted(item.JournalGenre.RowId);
			handler.AppendLiteral(" | ClassJobCategory=");
			handler.AppendFormatted(item.ClassJobCategory0.RowId);
			stringBuilder6.AppendLine(ref handler);
			stringBuilder=stringBuilder3;
			StringBuilder stringBuilder7=stringBuilder;
			handler=new StringBuilder.AppendInterpolatedStringHandler(16,1,stringBuilder);
			handler.AppendLiteral("  PreviousQuest=");
			handler.AppendFormatted(string.Join(',',from previous in item.PreviousQuest
				select previous.RowId into rowId
				where rowId!=0
				select rowId));
			stringBuilder7.AppendLine(ref handler);
			if(item.IssuerLocation.RowId!=0) {
				Level value2=item.IssuerLocation.Value;
				string value3=(DalamudApi.DataManager.GetExcelSheet<ENpcResident>().TryGetRow(item.IssuerStart.RowId,out var row)?row.Singular.ExtractText():string.Empty);
				string value4=value2.Territory.Value.PlaceName.Value.Name.ExtractText();
				stringBuilder=stringBuilder3;
				StringBuilder stringBuilder8=stringBuilder;
				handler=new StringBuilder.AppendInterpolatedStringHandler(44,3,stringBuilder);
				handler.AppendLiteral("  Quest giver=");
				handler.AppendFormatted(value3);
				handler.AppendLiteral(" | TerritoryType=");
				handler.AppendFormatted(value2.Territory.RowId);
				handler.AppendLiteral(" | Map.RowId=");
				handler.AppendFormatted(value2.Map.RowId);
				stringBuilder8.AppendLine(ref handler);
				stringBuilder=stringBuilder3;
				StringBuilder stringBuilder9=stringBuilder;
				handler=new StringBuilder.AppendInterpolatedStringHandler(36,4,stringBuilder);
				handler.AppendLiteral("  World position: X=");
				handler.AppendFormatted(value2.X,"0.###");
				handler.AppendLiteral(", Y=");
				handler.AppendFormatted(value2.Y,"0.###");
				handler.AppendLiteral(", Z=");
				handler.AppendFormatted(value2.Z,"0.###");
				handler.AppendLiteral(" | zone=");
				handler.AppendFormatted(value4);
				stringBuilder9.AppendLine(ref handler);
			}
		}
		if(array.Length==0) {
			stringBuilder3.AppendLine("No matches found.");
		}
		else if(array.Length>200) {
			stringBuilder=stringBuilder3;
			StringBuilder stringBuilder10=stringBuilder;
			handler=new StringBuilder.AppendInterpolatedStringHandler(56,1,stringBuilder);
			handler.AppendLiteral("More than ");
			handler.AppendFormatted(200);
			handler.AppendLiteral(" results - please use a more specific keyword.");
			stringBuilder10.AppendLine(ref handler);
		}
		return stringBuilder3.ToString().TrimEnd();
	}
	public string FindBeastmasterQuestChain() {
		Quest[] array=(from quest2 in DalamudApi.DataManager.GetExcelSheet<Quest>()
			where quest2.RowId!=0&&(quest2.JournalGenre.RowId==198||quest2.ClassJobCategory0.RowId==203)
			orderby quest2.RowId
			select quest2).ToArray();
		StringBuilder stringBuilder=new StringBuilder().AppendLine("Type: Beastmaster quest chain candidates");
		StringBuilder stringBuilder2=stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(45,2,stringBuilder);
		handler.AppendLiteral("Condition: JournalGenre=");
		handler.AppendFormatted(198u);
		handler.AppendLiteral(" or ClassJobCategory=");
		handler.AppendFormatted(203u);
		StringBuilder stringBuilder3=stringBuilder2.AppendLine(ref handler);
		Quest[] array2=array;
		for(int num=0;num<array2.Length;num++) {
			Quest quest=array2[num];
			stringBuilder=stringBuilder3;
			StringBuilder stringBuilder4=stringBuilder;
			handler=new StringBuilder.AppendInterpolatedStringHandler(9,2,stringBuilder);
			handler.AppendLiteral("RowId=");
			handler.AppendFormatted(quest.RowId);
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(quest.Name.ExtractText());
			stringBuilder4.AppendLine(ref handler);
			stringBuilder=stringBuilder3;
			StringBuilder stringBuilder5=stringBuilder;
			handler=new StringBuilder.AppendInterpolatedStringHandler(35,2,stringBuilder);
			handler.AppendLiteral("  JournalGenre=");
			handler.AppendFormatted(quest.JournalGenre.RowId);
			handler.AppendLiteral(" | ClassJobCategory=");
			handler.AppendFormatted(quest.ClassJobCategory0.RowId);
			stringBuilder5.AppendLine(ref handler);
			stringBuilder=stringBuilder3;
			StringBuilder stringBuilder6=stringBuilder;
			handler=new StringBuilder.AppendInterpolatedStringHandler(16,1,stringBuilder);
			handler.AppendLiteral("  PreviousQuest=");
			handler.AppendFormatted(string.Join(',',from previous in quest.PreviousQuest
				select previous.RowId into rowId
				where rowId!=0
				select rowId));
			stringBuilder6.AppendLine(ref handler);
		}
		if(array.Length==0) {
			stringBuilder3.AppendLine("No matches found.");
		}
		return stringBuilder3.ToString().TrimEnd();
	}
	public string FindItems(string query) {
		return FormatMatches("Item",query,from row in DalamudApi.DataManager.GetExcelSheet<Item>()
			select (RowId: row.RowId,Name: row.Name.ExtractText()));
	}
	public string FindNpcs(string query) {
		return FormatMatches("NPC ENpcResident",query,from row in DalamudApi.DataManager.GetExcelSheet<ENpcResident>()
			select (RowId: row.RowId,Name: row.Singular.ExtractText()));
	}
	public string FindMonsters(string query) {
		return FormatMatches("Monster BNpcName",query,from row in DalamudApi.DataManager.GetExcelSheet<BNpcName>()
			select (RowId: row.RowId,Name: row.Singular.ExtractText()));
	}
	public string FindDuties(string query) {
		return FormatMatches("Duty ContentFinderCondition",query,from row in DalamudApi.DataManager.GetExcelSheet<ContentFinderCondition>()
			select (RowId: row.RowId,Name: row.Name.ExtractText()));
	}
	public string FindCatalogDuties() {
		ExcelSheet<ContentFinderCondition> excelSheet=DalamudApi.DataManager.GetExcelSheet<ContentFinderCondition>();
		StringBuilder stringBuilder=new StringBuilder().AppendLine("Type: catalogue duty IDs").AppendLine("Source: BeastmasterCatalog.Duty");
		foreach(BeastmasterCatalogEntry item in BeastmasterCatalog.Entries.Where((BeastmasterCatalogEntry entry) => entry.LocationType==BeastmasterCatalogLocationType.Duty)) {
			string normalizedName=NormalizeDutyName(item.Location);
			ContentFinderCondition[] array=(from duty in excelSheet
				where duty.RowId!=0&&duty.TerritoryType.RowId!=0&&NormalizeDutyName(duty.Name.ExtractText()).Equals(normalizedName,StringComparison.Ordinal)
				orderby duty.RowId
				select duty).ToArray();
			StringBuilder stringBuilder2=stringBuilder;
			StringBuilder stringBuilder3=stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(20,3,stringBuilder2);
			handler.AppendLiteral("Catalogue ");
			handler.AppendFormatted(item.Number);
			handler.AppendLiteral(". ");
			handler.AppendFormatted(item.Name);
			handler.AppendLiteral(" | Duty=");
			handler.AppendFormatted(item.Location);
			stringBuilder3.AppendLine(ref handler);
			if(array.Length==0) {
				stringBuilder.AppendLine("  No matching ContentFinderCondition found.");
				continue;
			}
			ContentFinderCondition[] array2=array;
			for(int num=0;num<array2.Length;num++) {
				ContentFinderCondition contentFinderCondition=array2[num];
				string value=(DalamudApi.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(contentFinderCondition.TerritoryType.RowId,out var row)?row.PlaceName.Value.Name.ExtractText():string.Empty);
				stringBuilder2=stringBuilder;
				StringBuilder stringBuilder4=stringBuilder2;
				handler=new StringBuilder.AppendInterpolatedStringHandler(69,4,stringBuilder2);
				handler.AppendLiteral("  ContentFinderCondition.RowId=");
				handler.AppendFormatted(contentFinderCondition.RowId);
				handler.AppendLiteral(" | TerritoryType=");
				handler.AppendFormatted(contentFinderCondition.TerritoryType.RowId);
				handler.AppendLiteral(" | Map.RowId=");
				handler.AppendFormatted(contentFinderCondition.TerritoryType.Value.Map.RowId);
				handler.AppendLiteral(" | zone=");
				handler.AppendFormatted(value);
				stringBuilder4.AppendLine(ref handler);
			}
		}
		return stringBuilder.ToString().TrimEnd();
	}
	public string FindAutoCaptureData() {
		uint[] array=new uint[4] { 44879u, 44883u, 44885u, 44880u };
		ExcelSheet<Lumina.Excel.Sheets.Action> excelSheet=DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
		ExcelSheet<Lumina.Excel.Sheets.Status> excelSheet2=DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();
		StringBuilder stringBuilder=new StringBuilder().AppendLine("Type: auto-capture action and status IDs").AppendLine("Action:");
		uint[] array2=array;
		foreach(uint num in array2) {
			StringBuilder stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler;
			if(!excelSheet.TryGetRow(num,out var row)) {
				stringBuilder2=stringBuilder;
				StringBuilder stringBuilder3=stringBuilder2;
				handler=new StringBuilder.AppendInterpolatedStringHandler(22,1,stringBuilder2);
				handler.AppendLiteral("  ActionId ");
				handler.AppendFormatted(num);
				handler.AppendLiteral(": not found");
				stringBuilder3.AppendLine(ref handler);
				continue;
			}
			stringBuilder2=stringBuilder;
			StringBuilder stringBuilder4=stringBuilder2;
			handler=new StringBuilder.AppendInterpolatedStringHandler(47,5,stringBuilder2);
			handler.AppendLiteral("  ");
			handler.AppendFormatted(row.Name.ExtractText());
			handler.AppendLiteral(": Action.RowId=");
			handler.AppendFormatted(row.RowId);
			handler.AppendLiteral(" | ClassJob=");
			handler.AppendFormatted(row.ClassJob.RowId);
			handler.AppendLiteral(" | level=");
			handler.AppendFormatted(row.ClassJobLevel);
			handler.AppendLiteral(" | range=");
			handler.AppendFormatted(row.Range);
			stringBuilder4.AppendLine(ref handler);
		}
		stringBuilder.AppendLine("Status:");
		Lumina.Excel.Sheets.Status[] array3=(from status2 in excelSheet2.Where((Lumina.Excel.Sheets.Status status2) => {
				bool flag=status2.RowId!=0;
				if(flag) {
					uint rowId=status2.RowId;
					bool flag2=((rowId==4624||rowId==4626)?true:false);
					flag=flag2;
				}
				return flag;
			})
			orderby status2.RowId
			select status2).ToArray();
		if(array3.Length==0) {
			stringBuilder.AppendLine("  Capture: not found");
		}
		else {
			Lumina.Excel.Sheets.Status[] array4=array3;
			foreach(Lumina.Excel.Sheets.Status status in array4) {
				StringBuilder stringBuilder2=stringBuilder;
				StringBuilder stringBuilder5=stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(24,1,stringBuilder2);
				handler.AppendLiteral("  Capture: Status.RowId=");
				handler.AppendFormatted(status.RowId);
				stringBuilder5.AppendLine(ref handler);
			}
		}
		stringBuilder.AppendLine("Current target status:");
		if(!(DalamudApi.TargetManager.Target is IBattleChara battleChara)) {
			stringBuilder.AppendLine("  No combat target selected.");
		}
		else if(!battleChara.StatusList.Any()) {
			StringBuilder stringBuilder2=stringBuilder;
			StringBuilder stringBuilder6=stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(16,1,stringBuilder2);
			handler.AppendLiteral("  ");
			handler.AppendFormatted(battleChara.Name.TextValue);
			handler.AppendLiteral(": no statuses.");
			stringBuilder6.AppendLine(ref handler);
		}
		else {
			foreach(IStatus item in battleChara.StatusList.OrderBy((IStatus status2) => status2.StatusId)) {
				string value=(excelSheet2.TryGetRow(item.StatusId,out var row2)?row2.Name.ExtractText():string.Empty);
				StringBuilder stringBuilder2=stringBuilder;
				StringBuilder stringBuilder7=stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(40,4,stringBuilder2);
				handler.AppendLiteral("  StatusId=");
				handler.AppendFormatted(item.StatusId);
				handler.AppendLiteral(" | ");
				handler.AppendFormatted(value);
				handler.AppendLiteral(" | remaining=");
				handler.AppendFormatted(item.RemainingTime,"0.0");
				handler.AppendLiteral("s | SourceId=");
				handler.AppendFormatted(item.SourceId);
				stringBuilder7.AppendLine(ref handler);
			}
		}
		return stringBuilder.ToString().TrimEnd();
	}
	public string FindBeastmasterAttributes() {
		ExcelSheet<Lumina.Excel.Sheets.Action> excelSheet=DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
		StringBuilder stringBuilder=new StringBuilder().AppendLine("Type: Beastmaster familiar attribute map").AppendLine("Source: catalogue number + summon DataId + ultimate Action IconId").AppendLine("Attribute IconId: 3906=Fierce, 3907=Sturdy, 3908=Arcane, 3909=Winged")
			.AppendLine();
		for(int i=1;i<=BeastmasterCatalog.Entries.Count;i++) {
			BeastmasterCatalogEntry beastmasterCatalogEntry=BeastmasterCatalog.Entries[i-1];
			uint num=(uint)(18915+i);
			bool flag=TryGetSummonSkills(num,out var ultimateId,out var releaseId);
			Lumina.Excel.Sheets.Action action=((flag&&excelSheet.TryGetRow(ultimateId,out var row))?row:default(Lumina.Excel.Sheets.Action));
			Lumina.Excel.Sheets.Action action2=((flag&&excelSheet.TryGetRow(releaseId,out var row2))?row2:default(Lumina.Excel.Sheets.Action));
			int num2=(flag?action.Icon:0);
			string value=num2 switch {
				3906 => "Fierce",
				3907 => "Sturdy",
				3908 => "Arcane",
				3909 => "Winged",
				_ => "Unknown",
			};
			StringBuilder stringBuilder2=stringBuilder;
			StringBuilder stringBuilder3=stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(23,3,stringBuilder2);
			handler.AppendLiteral("Catalogue ");
			handler.AppendFormatted(beastmasterCatalogEntry.Number,"00");
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(beastmasterCatalogEntry.Name);
			handler.AppendLiteral(" | DataId=");
			handler.AppendFormatted(num);
			stringBuilder3.AppendLine(ref handler);
			stringBuilder2=stringBuilder;
			StringBuilder stringBuilder4=stringBuilder2;
			handler=new StringBuilder.AppendInterpolatedStringHandler(46,4,stringBuilder2);
			handler.AppendLiteral("  Ultimate ActionId=");
			handler.AppendFormatted(ultimateId);
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(GetActionName(action));
			handler.AppendLiteral(" | IconId=");
			handler.AppendFormatted(num2);
			handler.AppendLiteral(" | attribute=");
			handler.AppendFormatted(value);
			stringBuilder4.AppendLine(ref handler);
			stringBuilder2=stringBuilder;
			StringBuilder stringBuilder5=stringBuilder2;
			handler=new StringBuilder.AppendInterpolatedStringHandler(22,2,stringBuilder2);
			handler.AppendLiteral("  Release ActionId=");
			handler.AppendFormatted(releaseId);
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(GetActionName(action2));
			stringBuilder5.AppendLine(ref handler);
		}
		return stringBuilder.ToString().TrimEnd();
	}
	public string FindRecommendedEquipmentIds() {
		ExcelSheet<Item> excelSheet=DalamudApi.DataManager.GetExcelSheet<Item>();
		StringBuilder stringBuilder=new StringBuilder().AppendLine("Type: recommended gear item IDs").AppendLine("Note: the equipment page detects ownership by fixed ItemId").AppendLine();
		(string,IReadOnlyList<BeastmasterEquipmentEntry>)[] array=new (string,IReadOnlyList<BeastmasterEquipmentEntry>)[2] {
			("Starter set",BeastmasterEquipmentGuide.Level50Starter),
			("BIS",BeastmasterEquipmentGuide.Level50BestInSlot)
		};
		for(int i=0;i<array.Length;i++) {
			(string,IReadOnlyList<BeastmasterEquipmentEntry>) tuple=array[i];
			StringBuilder stringBuilder2=stringBuilder;
			StringBuilder stringBuilder3=stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(2,1,stringBuilder2);
			handler.AppendLiteral("[");
			handler.AppendFormatted(tuple.Item1);
			handler.AppendLiteral("]");
			stringBuilder3.AppendLine(ref handler);
			foreach(BeastmasterEquipmentEntry equipment in tuple.Item2.Where((BeastmasterEquipmentEntry entry) => entry.Name!="No equipment")) {
				Item[] array2=(from item3 in excelSheet
					where item3.RowId!=0&&item3.Name.ExtractText().Equals(equipment.Name,StringComparison.Ordinal)
					orderby item3.RowId
					select item3).ToArray();
				stringBuilder2=stringBuilder;
				StringBuilder stringBuilder4=stringBuilder2;
				handler=new StringBuilder.AppendInterpolatedStringHandler(3,2,stringBuilder2);
				handler.AppendFormatted(equipment.Slot);
				handler.AppendLiteral(" | ");
				handler.AppendFormatted(equipment.Name);
				stringBuilder4.AppendLine(ref handler);
				Item[] array3;
				if(array2.Length!=0) {
					array3=array2;
					for(int num=0;num<array3.Length;num++) {
						Item item=array3[num];
						stringBuilder2=stringBuilder;
						StringBuilder stringBuilder5=stringBuilder2;
						handler=new StringBuilder.AppendInterpolatedStringHandler(25,2,stringBuilder2);
						handler.AppendLiteral("  Exact match: ItemId=");
						handler.AppendFormatted(item.RowId);
						handler.AppendLiteral(" | ");
						handler.AppendFormatted(item.Name.ExtractText());
						stringBuilder5.AppendLine(ref handler);
					}
					continue;
				}
				Item[] array4=(from item3 in excelSheet
					where item3.RowId!=0&&item3.Name.ExtractText().Contains(equipment.Name,StringComparison.OrdinalIgnoreCase)
					orderby item3.RowId
					select item3).Take(10).ToArray();
				if(array4.Length==0) {
					stringBuilder.AppendLine("  No exact match or candidate found");
					continue;
				}
				array3=array4;
				for(int num=0;num<array3.Length;num++) {
					Item item2=array3[num];
					stringBuilder2=stringBuilder;
					StringBuilder stringBuilder6=stringBuilder2;
					handler=new StringBuilder.AppendInterpolatedStringHandler(23,2,stringBuilder2);
					handler.AppendLiteral("  Candidate: ItemId=");
					handler.AppendFormatted(item2.RowId);
					handler.AppendLiteral(" | ");
					handler.AppendFormatted(item2.Name.ExtractText());
					stringBuilder6.AppendLine(ref handler);
				}
			}
			stringBuilder.AppendLine();
		}
		return stringBuilder.ToString().TrimEnd();
	}
	public string GetCurrentTargetDebug() {
		if(!(DalamudApi.TargetManager.Target is IBattleChara battleChara)) {
			return "Type: current target\nNo valid BattleNpc target.";
		}
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		ExcelSheet<Lumina.Excel.Sheets.Status> excelSheet=DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();
		StringBuilder stringBuilder=new StringBuilder().AppendLine("Type: current target");
		StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(6,1,stringBuilder);
		handler.AppendLiteral("Name: ");
		handler.AppendFormatted(battleChara.Name.TextValue);
		StringBuilder stringBuilder2=stringBuilder.AppendLine(ref handler);
		StringBuilder.AppendInterpolatedStringHandler handler2=new StringBuilder.AppendInterpolatedStringHandler(10,1,stringBuilder2);
		handler2.AppendLiteral("EntityId: ");
		handler2.AppendFormatted(battleChara.EntityId);
		StringBuilder stringBuilder3=stringBuilder2.AppendLine(ref handler2);
		StringBuilder.AppendInterpolatedStringHandler handler3=new StringBuilder.AppendInterpolatedStringHandler(8,1,stringBuilder3);
		handler3.AppendLiteral("BaseId: ");
		handler3.AppendFormatted(battleChara.BaseId);
		StringBuilder stringBuilder4=stringBuilder3.AppendLine(ref handler3);
		StringBuilder.AppendInterpolatedStringHandler handler4=new StringBuilder.AppendInterpolatedStringHandler(7,2,stringBuilder4);
		handler4.AppendLiteral("HP: ");
		handler4.AppendFormatted(battleChara.CurrentHp);
		handler4.AppendLiteral(" / ");
		handler4.AppendFormatted(battleChara.MaxHp);
		StringBuilder stringBuilder5=stringBuilder4.AppendLine(ref handler4);
		StringBuilder.AppendInterpolatedStringHandler handler5=new StringBuilder.AppendInterpolatedStringHandler(5,1,stringBuilder5);
		handler5.AppendLiteral("HP: ");
		handler5.AppendFormatted((battleChara.MaxHp==0)?0f:((float)battleChara.CurrentHp*100f/(float)battleChara.MaxHp),"0.##");
		handler5.AppendLiteral("%");
		StringBuilder stringBuilder6=stringBuilder5.AppendLine(ref handler5);
		StringBuilder.AppendInterpolatedStringHandler handler6=new StringBuilder.AppendInterpolatedStringHandler(12,1,stringBuilder6);
		handler6.AppendLiteral("Targetable: ");
		handler6.AppendFormatted(battleChara.IsTargetable);
		StringBuilder stringBuilder7=stringBuilder6.AppendLine(ref handler6);
		StringBuilder stringBuilder8=stringBuilder7;
		StringBuilder.AppendInterpolatedStringHandler handler7=new StringBuilder.AppendInterpolatedStringHandler(6,1,stringBuilder7);
		handler7.AppendLiteral("Dead: ");
		handler7.AppendFormatted(battleChara.IsDead);
		StringBuilder stringBuilder9=stringBuilder8.AppendLine(ref handler7).AppendLine("Status:");
		foreach(IStatus item in battleChara.StatusList.OrderBy((IStatus status) => status.StatusId)) {
			string value=(excelSheet.TryGetRow(item.StatusId,out var row)?row.Name.ExtractText():"");
			string value2=((localPlayer!=null&&item.SourceId==localPlayer.EntityId)?"Self":"Other / unknown");
			stringBuilder7=stringBuilder9;
			StringBuilder stringBuilder10=stringBuilder7;
			handler=new StringBuilder.AppendInterpolatedStringHandler(50,5,stringBuilder7);
			handler.AppendLiteral("  StatusId=");
			handler.AppendFormatted(item.StatusId);
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(value);
			handler.AppendLiteral(" | SourceId=");
			handler.AppendFormatted(item.SourceId);
			handler.AppendLiteral(" | source=");
			handler.AppendFormatted(value2);
			handler.AppendLiteral(" | remaining=");
			handler.AppendFormatted(item.RemainingTime,"0.0");
			handler.AppendLiteral("s");
			stringBuilder10.AppendLine(ref handler);
		}
		return stringBuilder9.ToString().TrimEnd();
	}
	public unsafe string GetComboDebug() {
		ActionManager* ptr=ActionManager.Instance();
		if(ptr==null) {
			return "Type: current combo\nActionManager unavailable.";
		}
		StringBuilder stringBuilder=new StringBuilder().AppendLine("Type: current combo").AppendLine("Mode: read-only");
		StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(14,1,stringBuilder);
		handler.AppendLiteral("Combo.Action: ");
		handler.AppendFormatted(ptr->Combo.Action);
		StringBuilder stringBuilder2=stringBuilder.AppendLine(ref handler);
		StringBuilder.AppendInterpolatedStringHandler handler2=new StringBuilder.AppendInterpolatedStringHandler(14,1,stringBuilder2);
		handler2.AppendLiteral("Combo.Timer: ");
		handler2.AppendFormatted(ptr->Combo.Timer,"0.000");
		handler2.AppendLiteral("s");
		return stringBuilder2.AppendLine(ref handler2).AppendLine("Note: a Timer above 0 means the combo window is still open.").ToString()
			.TrimEnd();
	}
	public unsafe string GetCooperationValidationDebug() {
		BeastmasterGaugeSnapshot beastmasterGaugeSnapshot=BeastmasterGaugeSnapshot.ReadRaw();
		IBattleChara battleChara=DalamudApi.TargetManager.Target as IBattleChara;
		ActionManager* ptr=ActionManager.Instance();
		StringBuilder stringBuilder=new StringBuilder().AppendLine("Type: Beastmaster cooperation verification data").AppendLine("Mode: read-only, never uses actions");
		StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(7,1,stringBuilder);
		handler.AppendLiteral("Gauge: ");
		handler.AppendFormatted(beastmasterGaugeSnapshot.Available?"Available":beastmasterGaugeSnapshot.Status);
		StringBuilder stringBuilder2=stringBuilder.AppendLine(ref handler);
		StringBuilder.AppendInterpolatedStringHandler handler2=new StringBuilder.AppendInterpolatedStringHandler(14,1,stringBuilder2);
		handler2.AppendLiteral("Tenacity: ");
		handler2.AppendFormatted(beastmasterGaugeSnapshot.Tp);
		handler2.AppendLiteral("/250");
		StringBuilder stringBuilder3=stringBuilder2.AppendLine(ref handler2);
		StringBuilder.AppendInterpolatedStringHandler handler3=new StringBuilder.AppendInterpolatedStringHandler(17,1,stringBuilder3);
		handler3.AppendLiteral("Beast Power: ");
		handler3.AppendFormatted(beastmasterGaugeSnapshot.BeastPower);
		handler3.AppendLiteral("/250");
		StringBuilder stringBuilder4=stringBuilder3.AppendLine(ref handler3);
		StringBuilder.AppendInterpolatedStringHandler handler4=new StringBuilder.AppendInterpolatedStringHandler(20,1,stringBuilder4);
		handler4.AppendLiteral("Beast Heart: ");
		handler4.AppendFormatted(beastmasterGaugeSnapshot.BeastHeartStacks);
		handler4.AppendLiteral(" stacks");
		StringBuilder stringBuilder5=stringBuilder4.AppendLine(ref handler4);
		StringBuilder.AppendInterpolatedStringHandler handler5=new StringBuilder.AppendInterpolatedStringHandler(19,1,stringBuilder5);
		handler5.AppendLiteral("Beast Soul: ");
		handler5.AppendFormatted(beastmasterGaugeSnapshot.BeastSoulStacks);
		handler5.AppendLiteral(" stacks");
		StringBuilder stringBuilder6=stringBuilder5.AppendLine(ref handler5);
		StringBuilder stringBuilder7=stringBuilder6;
		StringBuilder.AppendInterpolatedStringHandler handler6=new StringBuilder.AppendInterpolatedStringHandler(17,1,stringBuilder6);
		handler6.AppendLiteral("Current whistle: ");
		byte whistleIndex=beastmasterGaugeSnapshot.WhistleIndex;
		handler6.AppendFormatted((whistleIndex>=1&&whistleIndex<=3)?$"no. {beastmasterGaugeSnapshot.WhistleIndex}":"Not summoned");
		StringBuilder stringBuilder8=stringBuilder7.AppendLine(ref handler6);
		BeastmasterCatalogEntry summonEntry=beastmasterGaugeSnapshot.SummonEntry;
		if(summonEntry==null) {
			stringBuilder8.AppendLine("Current familiar: not identified");
		}
		else {
			stringBuilder6=stringBuilder8;
			StringBuilder stringBuilder9=stringBuilder6;
			handler=new StringBuilder.AppendInterpolatedStringHandler(18,1,stringBuilder6);
			handler.AppendLiteral("Current familiar: ");
			handler.AppendFormatted(summonEntry.Name);
			stringBuilder9.AppendLine(ref handler);
			stringBuilder6=stringBuilder8;
			StringBuilder stringBuilder10=stringBuilder6;
			handler=new StringBuilder.AppendInterpolatedStringHandler(11,1,stringBuilder6);
			handler.AppendLiteral("Attribute: ");
			handler.AppendFormatted(summonEntry.Attribute);
			stringBuilder10.AppendLine(ref handler);
			stringBuilder6=stringBuilder8;
			StringBuilder stringBuilder11=stringBuilder6;
			handler=new StringBuilder.AppendInterpolatedStringHandler(22,2,stringBuilder6);
			handler.AppendLiteral("Familiar ultimate: ");
			handler.AppendFormatted(summonEntry.UltimateActionId);
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(GetActionNameById(summonEntry.UltimateActionId));
			stringBuilder11.AppendLine(ref handler);
			stringBuilder6=stringBuilder8;
			StringBuilder stringBuilder12=stringBuilder6;
			handler=new StringBuilder.AppendInterpolatedStringHandler(12,2,stringBuilder6);
			handler.AppendLiteral("Release: ");
			handler.AppendFormatted(summonEntry.ReleaseActionId);
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(GetActionNameById(summonEntry.ReleaseActionId));
			stringBuilder12.AppendLine(ref handler);
		}
		if(battleChara==null) {
			stringBuilder8.AppendLine("Target: no valid BattleNpc");
		}
		else {
			stringBuilder6=stringBuilder8;
			StringBuilder stringBuilder13=stringBuilder6;
			handler=new StringBuilder.AppendInterpolatedStringHandler(30,3,stringBuilder6);
			handler.AppendLiteral("Target: ");
			handler.AppendFormatted(battleChara.Name.TextValue);
			handler.AppendLiteral(" | EntityId=");
			handler.AppendFormatted(battleChara.EntityId);
			handler.AppendLiteral(" | BaseId=");
			handler.AppendFormatted(battleChara.BaseId);
			stringBuilder13.AppendLine(ref handler);
			stringBuilder6=stringBuilder8;
			StringBuilder stringBuilder14=stringBuilder6;
			handler=new StringBuilder.AppendInterpolatedStringHandler(34,4,stringBuilder6);
			handler.AppendLiteral("Target HP: ");
			handler.AppendFormatted(battleChara.CurrentHp);
			handler.AppendLiteral("/");
			handler.AppendFormatted(battleChara.MaxHp);
			handler.AppendLiteral(" | targetable=");
			handler.AppendFormatted(battleChara.IsTargetable);
			handler.AppendLiteral(" | dead=");
			handler.AppendFormatted(battleChara.IsDead);
			stringBuilder14.AppendLine(ref handler);
		}
		if(ptr==null||battleChara==null) {
			stringBuilder8.AppendLine("Action status: ActionManager or target unavailable");
		}
		else {
			stringBuilder8.AppendLine("Action status:");
			uint[] array=new uint[5] { 47093u, 44884u, 44887u, 44888u, 44889u };
			foreach(uint num in array) {
				uint actionStatus=ptr->GetActionStatus(ActionType.Action,num,battleChara.GameObjectId,checkRecastActive: true,checkCastingActive: true,null);
				stringBuilder6=stringBuilder8;
				StringBuilder stringBuilder15=stringBuilder6;
				handler=new StringBuilder.AppendInterpolatedStringHandler(24,3,stringBuilder6);
				handler.AppendLiteral("  ActionId=");
				handler.AppendFormatted(num);
				handler.AppendLiteral(" | ");
				handler.AppendFormatted(GetActionNameById(num));
				handler.AppendLiteral(" | status=");
				handler.AppendFormatted(actionStatus);
				stringBuilder15.AppendLine(ref handler);
			}
		}
		stringBuilder8.AppendLine("Your own statuses:");
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			stringBuilder8.AppendLine("  Local character unavailable");
		}
		else {
			foreach(IStatus item in localPlayer.StatusList.Where((IStatus status) => {
				uint statusId=status.StatusId;
				return statusId>=4595&&statusId<=4598;
			})) {
				stringBuilder6=stringBuilder8;
				StringBuilder stringBuilder16=stringBuilder6;
				handler=new StringBuilder.AppendInterpolatedStringHandler(37,3,stringBuilder6);
				handler.AppendLiteral("  StatusId=");
				handler.AppendFormatted(item.StatusId);
				handler.AppendLiteral(" | SourceId=");
				handler.AppendFormatted(item.SourceId);
				handler.AppendLiteral(" | remaining=");
				handler.AppendFormatted(item.RemainingTime,"0.0");
				handler.AppendLiteral("s");
				stringBuilder16.AppendLine(ref handler);
			}
		}
		return stringBuilder8.ToString().TrimEnd();
	}
	private static bool TryGetSummonSkills(uint dataId,out uint ultimateId,out uint releaseId) {
		int num=(int)(dataId-18915);
		if((num<1||num>50)?true:false) {
			ultimateId=0u;
			releaseId=0u;
			return false;
		}
		ultimateId=(uint)(44933+num*2);
		releaseId=ultimateId+1;
		return true;
	}
	private static string GetActionName(Lumina.Excel.Sheets.Action action) {
		if(action.RowId!=0) {
			return action.Name.ExtractText();
		}
		return "Not found";
	}
	private static string GetActionNameById(uint actionId) {
		if(!DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId,out var row)) {
			return "Not found";
		}
		return GetActionName(row);
	}
	public string GetBeastmasterGaugeRaw() {
		if(DalamudApi.PlayerState.ClassJob.RowId!=43) {
			return "Type: raw Beastmaster gauge\nSwitch to Beastmaster first.";
		}
		BeastmasterGaugeSnapshot beastmasterGaugeSnapshot=BeastmasterGaugeSnapshot.ReadRaw();
		if(!beastmasterGaugeSnapshot.Available) {
			return "Type: raw Beastmaster gauge\n"+beastmasterGaugeSnapshot.Status+".";
		}
		nint address=beastmasterGaugeSnapshot.Address;
		Span<byte> span=beastmasterGaugeSnapshot.Bytes.AsSpan();
		int length=span.Length;
		ushort[] array=new ushort[length/2];
		uint[] array2=new uint[length/4];
		for(int i=0;i<array.Length;i++) {
			array[i]=BitConverter.ToUInt16(span.Slice(i*2,2));
		}
		for(int j=0;j<array2.Length;j++) {
			array2[j]=BitConverter.ToUInt32(span.Slice(j*4,4));
		}
		StringBuilder stringBuilder=new StringBuilder().AppendLine("Type: raw Beastmaster gauge data").AppendLine("Mode: read-only, never writes to memory");
		StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(10,1,stringBuilder);
		handler.AppendLiteral("ClassJob: ");
		handler.AppendFormatted(43u);
		StringBuilder stringBuilder2=stringBuilder.AppendLine(ref handler);
		StringBuilder.AppendInterpolatedStringHandler handler2=new StringBuilder.AppendInterpolatedStringHandler(11,1,stringBuilder2);
		handler2.AppendLiteral("Address: 0x");
		handler2.AppendFormatted(((IntPtr)address).ToInt64(),"X");
		StringBuilder stringBuilder3=stringBuilder2.AppendLine(ref handler2);
		StringBuilder.AppendInterpolatedStringHandler handler3=new StringBuilder.AppendInterpolatedStringHandler(14,1,stringBuilder3);
		handler3.AppendLiteral("Length: ");
		handler3.AppendFormatted(length);
		handler3.AppendLiteral(" bytes");
		StringBuilder stringBuilder4=stringBuilder3.AppendLine(ref handler3);
		StringBuilder.AppendInterpolatedStringHandler handler4=new StringBuilder.AppendInterpolatedStringHandler(5,1,stringBuilder4);
		handler4.AppendLiteral("Hex: ");
		handler4.AppendFormatted(string.Join(' ',from value in span.ToArray()
			select value.ToString("X2")));
		StringBuilder stringBuilder5=stringBuilder4.AppendLine(ref handler4);
		StringBuilder.AppendInterpolatedStringHandler handler5=new StringBuilder.AppendInterpolatedStringHandler(8,1,stringBuilder5);
		handler5.AppendLiteral("UInt16: ");
		handler5.AppendFormatted(string.Join(' ',array));
		StringBuilder stringBuilder6=stringBuilder5.AppendLine(ref handler5);
		StringBuilder.AppendInterpolatedStringHandler handler6=new StringBuilder.AppendInterpolatedStringHandler(8,1,stringBuilder6);
		handler6.AppendLiteral("UInt32: ");
		handler6.AppendFormatted(string.Join(' ',array2));
		return stringBuilder6.AppendLine(ref handler6).ToString().TrimEnd();
	}
	private static string FormatMatches(string category,string query,IEnumerable<(uint RowId,string Name)> rows) {
		query=query.Trim();
		if(query.Length==0) {
			return category+"\nEnter a name keyword before reading.";
		}
		(uint,string)[] array=(from row in rows
			where row.RowId!=0&&!string.IsNullOrWhiteSpace(row.Name)&&row.Name.Contains(query,StringComparison.OrdinalIgnoreCase)
			orderby row.RowId
			select row).Take(201).ToArray();
		bool flag=array.Length>200;
		StringBuilder stringBuilder=new StringBuilder();
		StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(6,1,stringBuilder);
		handler.AppendLiteral("Type: ");
		handler.AppendFormatted(category);
		StringBuilder stringBuilder2=stringBuilder.AppendLine(ref handler);
		StringBuilder stringBuilder3=stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler2=new StringBuilder.AppendInterpolatedStringHandler(9,1,stringBuilder2);
		handler2.AppendLiteral("Keyword: ");
		handler2.AppendFormatted(query);
		StringBuilder stringBuilder4=stringBuilder3.AppendLine(ref handler2);
		foreach(var item in array.Take(200)) {
			stringBuilder4.Append("RowId=").Append(item.Item1).Append(" | ")
				.AppendLine(item.Item2);
		}
		if(array.Length==0) {
			stringBuilder4.AppendLine("No matches found.");
		}
		else if(flag) {
			stringBuilder2=stringBuilder4;
			StringBuilder stringBuilder5=stringBuilder2;
			handler=new StringBuilder.AppendInterpolatedStringHandler(56,1,stringBuilder2);
			handler.AppendLiteral("More than ");
			handler.AppendFormatted(200);
			handler.AppendLiteral(" results - please use a more specific keyword.");
			stringBuilder5.AppendLine(ref handler);
		}
		return stringBuilder4.ToString().TrimEnd();
	}
	private static string JoinLines(params string[] lines) {
		return string.Join(Environment.NewLine,lines);
	}
	private static string NormalizeDutyName(string name) {
		return new string(name.Where((char character) => !char.IsWhiteSpace(character)&&character!='·'&&character!='：'&&character!=':').ToArray());
	}
}
