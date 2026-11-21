using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;
namespace Beastmaster;

public sealed class BeastmasterQuestService {
	private readonly Dictionary<uint,BeastmasterQuestLocation?> issuerLocations=new Dictionary<uint,BeastmasterQuestLocation>();
	private readonly Dictionary<uint,IReadOnlyList<BeastmasterQuestPrerequisite>> prerequisites=new Dictionary<uint,IReadOnlyList<BeastmasterQuestPrerequisite>>();
	private readonly Dictionary<uint,byte> sequences=new Dictionary<uint,byte>();
	private readonly Dictionary<uint,BeastmasterQuestStatus> statuses=new Dictionary<uint,BeastmasterQuestStatus>();
	public BeastmasterQuestStatus GetStatus(uint questRowId) {
		return statuses.GetValueOrDefault(questRowId);
	}
	public byte GetSequence(uint questRowId) {
		return sequences.GetValueOrDefault(questRowId);
	}
	public bool TryGetCurrentTarget(uint questRowId,out BeastmasterQuestLocation location) {
		byte sequence=GetSequence(questRowId);
		BeastmasterQuestTarget beastmasterQuestTarget=BeastmasterQuestGuide.Targets.FirstOrDefault((BeastmasterQuestTarget candidate) => candidate.QuestRowId==questRowId&&candidate.Sequence==sequence);
		if(beastmasterQuestTarget==null) {
			location=null;
			return false;
		}
		location=new BeastmasterQuestLocation(beastmasterQuestTarget.TerritoryType,beastmasterQuestTarget.MapRowId,beastmasterQuestTarget.Position,beastmasterQuestTarget.Zone,beastmasterQuestTarget.Name);
		return true;
	}
	public IReadOnlyList<BeastmasterQuestPrerequisite> GetPrerequisites(uint questRowId) {
		if(prerequisites.TryGetValue(questRowId,out IReadOnlyList<BeastmasterQuestPrerequisite> value)) {
			return value;
		}
		if(!DalamudApi.DataManager.GetExcelSheet<Quest>().TryGetRow(questRowId,out var row)) {
			prerequisites[questRowId]=Array.Empty<BeastmasterQuestPrerequisite>();
			return prerequisites[questRowId];
		}
		ExcelSheet<Quest> questSheet=DalamudApi.DataManager.GetExcelSheet<Quest>();
		prerequisites[questRowId]=(from rowId in (from previous in row.PreviousQuest
				select previous.RowId into rowId
				where rowId!=0
				select rowId).Distinct()
			select new BeastmasterQuestPrerequisite(rowId,questSheet.TryGetRow(rowId,out var row2)?row2.Name.ExtractText():$"Quest {rowId}")).ToArray();
		return prerequisites[questRowId];
	}
	public unsafe void RefreshStatuses(IEnumerable<uint> questRowIds) {
		QuestManager* ptr=QuestManager.Instance();
		foreach(uint questRowId in questRowIds) {
			sequences[questRowId]=QuestManager.GetQuestSequence(questRowId);
			statuses[questRowId]=(QuestManager.IsQuestComplete(questRowId)?BeastmasterQuestStatus.Completed:((ptr!=null&&ptr->IsQuestAccepted(questRowId))?BeastmasterQuestStatus.Accepted:BeastmasterQuestStatus.NotAccepted));
		}
	}
	public unsafe string GetActiveQuestsDebug() {
		QuestManager* ptr=QuestManager.Instance();
		StringBuilder stringBuilder=new StringBuilder().AppendLine("Type: state of all normal quests");
		if(ptr==null) {
			stringBuilder.AppendLine("QuestManager: unavailable.");
			return stringBuilder.ToString().TrimEnd();
		}
		Dictionary<ushort,Quest> dictionary=(from quest in DalamudApi.DataManager.GetExcelSheet<Quest>()
			where quest.RowId!=0
			group quest by (ushort)(quest.RowId&0xFFFF)).ToDictionary((IGrouping<ushort,Quest> group) => group.Key,(IGrouping<ushort,Quest> group) => group.First());
		int num=0;
		Span<QuestWork> normalQuests=ptr->NormalQuests;
		StringBuilder stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler;
		for(int num2=0;num2<normalQuests.Length;num2++) {
			QuestWork questWork=normalQuests[num2];
			if(questWork.QuestId!=0) {
				num++;
				bool flag=dictionary.TryGetValue(questWork.QuestId,out var value);
				uint value2=(flag?value.RowId:questWork.QuestId);
				string value3=(flag?value.Name.ExtractText():"Unknown quest");
				stringBuilder2=stringBuilder;
				StringBuilder stringBuilder3=stringBuilder2;
				handler=new StringBuilder.AppendInterpolatedStringHandler(27,3,stringBuilder2);
				handler.AppendLiteral("RowId=");
				handler.AppendFormatted(value2);
				handler.AppendLiteral(" | RuntimeQuestId=");
				handler.AppendFormatted(questWork.QuestId);
				handler.AppendLiteral(" | ");
				handler.AppendFormatted(value3);
				stringBuilder3.AppendLine(ref handler);
				stringBuilder2=stringBuilder;
				StringBuilder stringBuilder4=stringBuilder2;
				handler=new StringBuilder.AppendInterpolatedStringHandler(38,3,stringBuilder2);
				handler.AppendLiteral("  Sequence=");
				handler.AppendFormatted(questWork.Sequence);
				handler.AppendLiteral(" | Flags=");
				handler.AppendFormatted(questWork.Flags);
				handler.AppendLiteral(" | AcceptClassJob=");
				handler.AppendFormatted(questWork.AcceptClassJob);
				stringBuilder4.AppendLine(ref handler);
				byte[] array=new byte[6];
				for(int num3=0;num3<array.Length;num3++) {
					array[num3]=questWork.Variables[num3];
				}
				stringBuilder2=stringBuilder;
				StringBuilder stringBuilder5=stringBuilder2;
				handler=new StringBuilder.AppendInterpolatedStringHandler(12,1,stringBuilder2);
				handler.AppendLiteral("  Variables=");
				handler.AppendFormatted(string.Join(',',array));
				stringBuilder5.AppendLine(ref handler);
			}
		}
		stringBuilder2=stringBuilder;
		StringBuilder stringBuilder6=stringBuilder2;
		handler=new StringBuilder.AppendInterpolatedStringHandler(12,1,stringBuilder2);
		handler.AppendLiteral("quest count=");
		handler.AppendFormatted(num);
		stringBuilder6.AppendLine(ref handler);
		return stringBuilder.ToString().TrimEnd();
	}
	public bool TryGetIssuerLocation(uint questRowId,out BeastmasterQuestLocation location) {
		if(issuerLocations.TryGetValue(questRowId,out BeastmasterQuestLocation value)) {
			location=value;
			return value!=null;
		}
		location=null;
		if(!DalamudApi.DataManager.GetExcelSheet<Quest>().TryGetRow(questRowId,out var row)||row.IssuerLocation.RowId==0) {
			issuerLocations[questRowId]=null;
			return false;
		}
		Level value2=row.IssuerLocation.Value;
		uint rowId=value2.Territory.RowId;
		uint rowId2=value2.Map.RowId;
		if(rowId==0||rowId2==0) {
			issuerLocations[questRowId]=null;
			return false;
		}
		string zone=value2.Territory.Value.PlaceName.Value.Name.ExtractText();
		string npcName=(DalamudApi.DataManager.GetExcelSheet<ENpcResident>().TryGetRow(row.IssuerStart.RowId,out var row2)?row2.Singular.ExtractText():string.Empty);
		location=new BeastmasterQuestLocation(rowId,rowId2,new Vector3(value2.X,value2.Y,value2.Z),zone,npcName);
		issuerLocations[questRowId]=location;
		return true;
	}
}
