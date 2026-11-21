using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
namespace Beastmaster;

public static class BeastmasterCaptureTargeting {
	private static readonly Dictionary<int,string[]> Aliases=new Dictionary<int,string[]> {
		[2]=new string[1] { "Ground Squirrel" },
		[3]=new string[1] { "Lost Lamb" },
		[6]=new string[1] { "Wild Dodo" },
		[7]=new string[1] { "Rusty Coblyn" },
		[10]=new string[1] { "Huge Hornet" },
		[11]=new string[1] { "Nesting Buzzard" },
		[12]=new string[1] { "Tiny Mandragora" },
		[14]=new string[1] { "Puk Hatchling" },
		[15]=new string[1] { "Thickshell" },
		[16]=new string[1] { "Killer Mantis" },
		[17]=new string[1] { "Ichorous Ire" },
		[18]=new string[1] { "Doctore" },
		[19]=new string[1] { "Cave Bat" },
		[20]=new string[1] { "Roselet" },
		[21]=new string[1] { "Rothlyt Pelican" },
		[22]=new string[2] { "Cactuar", "Cactaur" },
		[23]=new string[1] { "Sandstone Golem" },
		[25]=new string[1] { "Giant Tortoise" },
		[26]=new string[1] { "Wounded Aurochs" },
		[27]=new string[1] { "Scaphite" },
		[28]=new string[1] { "Sandworm" },
		[29]=new string[1] { "Spriggan Graverobber" },
		[30]=new string[1] { "Mossless Goobbue" },
		[31]=new string[1] { "Rivertoad" },
		[34]=new string[1] { "Anole" },
		[35]=new string[1] { "Sundrake" },
		[36]=new string[1] { "Treant Sapling" },
		[37]=new string[1] { "Myrmidon Princess" },
		[39]=new string[2] { "Stroper", "Halitostroper" },
		[40]=new string[1] { "Bogy" },
		[41]=new string[1] { "Black Eft" },
		[42]=new string[1] { "Lake Cobra" },
		[44]=new string[1] { "Gadfly" },
		[45]=new string[1] { "Decaying Gourmand" },
		[47]=new string[1] { "Wandil" },
		[50]=new string[1] { "King Behemoth" }
	};
	public static CaptureVerdict Evaluate(IBattleChara target,int playerLevel,ushort currentTerritory,BeastmasterProgressService? progressService) {
		if(BelongsToFate(target)) {
			return CaptureVerdict.No("FATE enemies cannot be captured");
		}
		IBattleNpc battleNpc=target as IBattleNpc;
		bool flag=battleNpc!=null;
		if(flag) {
			BattleNpcSubKind battleNpcKind=battleNpc.BattleNpcKind;
			bool flag2=((battleNpcKind==BattleNpcSubKind.Pet||battleNpcKind==BattleNpcSubKind.NpcPartyMember)?true:false);
			flag=flag2;
		}
		if(flag) {
			return CaptureVerdict.No($"Not a wild enemy ({battleNpc.BattleNpcKind})");
		}
		BeastmasterCatalogEntry beastmasterCatalogEntry=MatchCatalogueEntry(target.Name.TextValue,currentTerritory);
		if(beastmasterCatalogEntry==null) {
			return CaptureVerdict.No("Not a beast in your Bestiary's list");
		}
		if(BeastmasterTeam.IsUnlocked(beastmasterCatalogEntry.Number)==true) {
			return CaptureVerdict.No(beastmasterCatalogEntry.DisplayName+" is already in your Bestiary");
		}
		int? num=LowestLevel(beastmasterCatalogEntry.Level);
		if(num.HasValue) {
			int valueOrDefault=num.GetValueOrDefault();
			if(playerLevel<valueOrDefault) {
				return CaptureVerdict.No($"Below this beast's level range (you {playerLevel}, it starts at {valueOrDefault})");
			}
		}
		return CaptureVerdict.Yes;
	}
	private unsafe static bool BelongsToFate(IBattleChara target) {
		try {
			if(target.Address==IntPtr.Zero) {
				return false;
			}
			return ((BattleChara*)target.Address)->FateId!=0;
		}
		catch {
			return false;
		}
	}
	private static BeastmasterCatalogEntry? MatchCatalogueEntry(string targetName,ushort currentTerritory) {
		if(string.IsNullOrWhiteSpace(targetName)) {
			return null;
		}
		BeastmasterCatalogEntry beastmasterCatalogEntry=null;
		foreach(BeastmasterCatalogEntry entry in BeastmasterCatalog.Entries) {
			string displayName=entry.DisplayName;
			if(!string.IsNullOrWhiteSpace(displayName)&&(ContainsWholeWord(targetName,displayName)||(Aliases.TryGetValue(entry.Number,out string[] value)&&value.Any((string w) => ContainsWholeWord(targetName,w))))&&(entry.LocationType==BeastmasterCatalogLocationType.Duty||entry.TerritoryType==0||entry.TerritoryType==currentTerritory)&&(beastmasterCatalogEntry==null||displayName.Length>beastmasterCatalogEntry.DisplayName.Length)) {
				beastmasterCatalogEntry=entry;
			}
		}
		return beastmasterCatalogEntry;
	}
	public static string Describe(IBattleChara target,int playerLevel,ushort territory) {
		StringBuilder stringBuilder=new StringBuilder();
		string textValue=target.Name.TextValue;
		StringBuilder stringBuilder2=stringBuilder;
		StringBuilder stringBuilder3=stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(27,2,stringBuilder2);
		handler.AppendLiteral("Target: \"");
		handler.AppendFormatted(textValue);
		handler.AppendLiteral("\"  (normalised \"");
		handler.AppendFormatted(Normalise(textValue));
		handler.AppendLiteral("\")");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.AppendLine("Name code points: "+string.Join(" ",textValue.Select((char c) => $"U+{c:X4}")));
		stringBuilder.AppendLine($"Level {target.Level} (you {playerLevel}), territory {territory}, kind {target.ObjectKind}, "+((target is IBattleNpc battleNpc)?$"battle-npc kind {battleNpc.BattleNpcKind}, ":string.Empty)+$"data id {target.DataId}, entity id {target.EntityId}");
		stringBuilder2=stringBuilder;
		StringBuilder stringBuilder4=stringBuilder2;
		handler=new StringBuilder.AppendInterpolatedStringHandler(18,3,stringBuilder2);
		handler.AppendLiteral("HP ");
		handler.AppendFormatted(target.CurrentHp);
		handler.AppendLiteral("/");
		handler.AppendFormatted(target.MaxHp);
		handler.AppendLiteral(", FATE enemy: ");
		handler.AppendFormatted(BelongsToFate(target));
		stringBuilder4.AppendLine(ref handler);
		stringBuilder2=stringBuilder;
		StringBuilder stringBuilder5=stringBuilder2;
		handler=new StringBuilder.AppendInterpolatedStringHandler(29,1,stringBuilder2);
		handler.AppendLiteral("Capture already tried on it: ");
		handler.AppendFormatted(BeastmasterCaptureAttempts.AlreadyTried(target.GameObjectId));
		stringBuilder5.AppendLine(ref handler);
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("Catalogue entries whose name is in the target's name:");
		bool flag=false;
		foreach(BeastmasterCatalogEntry entry in BeastmasterCatalog.Entries) {
			if(ContainsWholeWord(textValue,entry.DisplayName)) {
				flag=true;
				stringBuilder2=stringBuilder;
				StringBuilder stringBuilder6=stringBuilder2;
				handler=new StringBuilder.AppendInterpolatedStringHandler(65,7,stringBuilder2);
				handler.AppendLiteral("  #");
				handler.AppendFormatted(entry.Number);
				handler.AppendLiteral(" \"");
				handler.AppendFormatted(entry.DisplayName);
				handler.AppendLiteral("\" [");
				handler.AppendFormatted(string.Join(" ",entry.DisplayName.Select((char c) => $"U+{c:X4}")));
				handler.AppendLiteral("] ");
				handler.AppendLiteral("type ");
				handler.AppendFormatted(entry.LocationType);
				handler.AppendLiteral(", territory ");
				handler.AppendFormatted(entry.TerritoryType);
				handler.AppendLiteral(" (you are in ");
				handler.AppendFormatted(territory);
				handler.AppendLiteral("), ");
				handler.AppendLiteral("client says unlocked: ");
				handler.AppendFormatted(BeastmasterTeam.IsUnlocked(entry.Number)?.ToString() ?? "unknown");
				stringBuilder6.AppendLine(ref handler);
			}
		}
		if(!flag) {
			stringBuilder.AppendLine("  none - and these are the catalogue names closest to it:");
			string first=Normalise(textValue).Split(' ')[0];
			foreach(BeastmasterCatalogEntry item in BeastmasterCatalog.Entries.Where((BeastmasterCatalogEntry e) => Normalise(e.DisplayName).Contains(first)||first.Contains(Normalise(e.DisplayName)))) {
				stringBuilder2=stringBuilder;
				StringBuilder stringBuilder7=stringBuilder2;
				handler=new StringBuilder.AppendInterpolatedStringHandler(9,3,stringBuilder2);
				handler.AppendLiteral("  #");
				handler.AppendFormatted(item.Number);
				handler.AppendLiteral(" \"");
				handler.AppendFormatted(item.DisplayName);
				handler.AppendLiteral("\" [");
				handler.AppendFormatted(string.Join(" ",item.DisplayName.Select((char c) => $"U+{c:X4}")));
				handler.AppendLiteral("]");
				stringBuilder7.AppendLine(ref handler);
			}
		}
		CaptureVerdict captureVerdict=Evaluate(target,playerLevel,territory,null);
		stringBuilder.AppendLine();
		stringBuilder2=stringBuilder;
		StringBuilder stringBuilder8=stringBuilder2;
		handler=new StringBuilder.AppendInterpolatedStringHandler(9,1,stringBuilder2);
		handler.AppendLiteral("Verdict: ");
		handler.AppendFormatted(captureVerdict.ShouldCapture?"capturable":("NOT capturable - "+captureVerdict.Reason));
		stringBuilder8.AppendLine(ref handler);
		stringBuilder2=stringBuilder;
		StringBuilder stringBuilder9=stringBuilder2;
		handler=new StringBuilder.AppendInterpolatedStringHandler(53,2,stringBuilder2);
		handler.AppendLiteral("Bestiary unlocked count from client: ");
		handler.AppendFormatted(BeastmasterTeam.UnlockedCount());
		handler.AppendLiteral(", mask trusted: ");
		handler.AppendFormatted(BeastmasterTeam.IsUnlocked(1).HasValue);
		stringBuilder9.AppendLine(ref handler);
		return stringBuilder.ToString();
	}
	public static bool NeedsHigherLevel(BeastmasterCatalogEntry entry,int playerLevel) {
		if(playerLevel>0) {
			int? num=LowestLevel(entry.Level);
			if(num.HasValue) {
				int valueOrDefault=num.GetValueOrDefault();
				return playerLevel<valueOrDefault;
			}
		}
		return false;
	}
	public static int? LowestLevel(string range) {
		int num=range.ToList().FindIndex(char.IsDigit);
		if(num<0) {
			return null;
		}
		int i;
		for(i=num;i<range.Length&&char.IsDigit(range[i]);i++) {
		}
		if(!int.TryParse(range.AsSpan(num,i-num),out var result)) {
			return null;
		}
		return result;
	}
	private static string Normalise(string text) {
		char[] array=new char[text.Length];
		int length=0;
		bool flag=true;
		foreach(char c in text) {
			if(char.IsLetterOrDigit(c)) {
				array[length++]=char.ToLowerInvariant(c);
				flag=false;
			}
			else if(!flag) {
				array[length++]=' ';
				flag=true;
			}
		}
		return new string(array,0,length).Trim();
	}
	private static bool ContainsWholeWord(string haystack,string needle) {
		haystack=Normalise(haystack);
		needle=Normalise(needle);
		if(needle.Length==0) {
			return false;
		}
		for(int num=haystack.IndexOf(needle,StringComparison.OrdinalIgnoreCase);num>=0;num=haystack.IndexOf(needle,num+1,StringComparison.OrdinalIgnoreCase)) {
			bool num2=num==0||!char.IsLetterOrDigit(haystack[num-1]);
			int num3=num+needle.Length;
			bool flag=num3>=haystack.Length||!char.IsLetterOrDigit(haystack[num3]);
			if(num2&flag) {
				return true;
			}
		}
		return false;
	}
}
