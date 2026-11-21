using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Text.ReadOnly;
namespace Beastmaster;

public static class FamiliarCombatData {
	private static Dictionary<int,FamiliarCombatProfile>? profiles;
	public static IReadOnlyDictionary<int,FamiliarCombatProfile> All => profiles ?? (profiles=Load());
	private static Dictionary<int,FamiliarCombatProfile> Load() {
		Dictionary<int,FamiliarCombatProfile> dictionary=new Dictionary<int,FamiliarCombatProfile>();
		ExcelSheet<RawRow> sheet=DalamudApi.DataManager.Excel.GetSheet<RawRow>(Language.English,"XBMPet");
		foreach(BeastmasterCatalogEntry entry in BeastmasterCatalog.Entries) {
			string text="";
			string text2="";
			try {
				if(sheet.TryGetRow((uint)entry.Number,out var row)&&row.ReadColumn(9) is ReadOnlySeString readOnlySeString&&row.ReadColumn(10) is ReadOnlySeString readOnlySeString2) {
					text=readOnlySeString.ExtractText();
					text2=readOnlySeString2.ExtractText();
				}
			}
			catch(Exception exception) {
				DalamudApi.Log.Warning(exception,"Familiar ability data unavailable for {Id}",entry.Number);
			}
			dictionary[entry.Number]=new FamiliarCombatProfile(entry.Number,entry.DisplayName,text,text2,CrucibleTeamPlanner.DamageFromDescription(text),CrucibleTeamPlanner.DamageFromDescription(text2),entry.UltimateActionId,entry.ReleaseActionId);
		}
		return dictionary;
	}
	public static string DescribeAll() {
		return string.Join("\n",All.Values.Select((FamiliarCombatProfile p) => $"{p.CatalogueId}: {p.Name}\nPrimary [{string.Join(", ",p.PrimaryDamage)}]: {p.Primary}\nSecondary [{string.Join(", ",p.SecondaryDamage)}]: {p.Secondary}\nUltimate={p.UltimateAction} Release={p.ReleaseAction}"));
	}
}
