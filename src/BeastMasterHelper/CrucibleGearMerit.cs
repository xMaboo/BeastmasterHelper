using System.Collections.Generic;
namespace Beastmaster;

public static class CrucibleGearMerit {
	private static readonly IReadOnlyDictionary<int,(float Score,string Why)> Merits=new Dictionary<int,(float,string)> {
		[3]=(110f,"restores 25% of maximum HP after every battle - more healing over a board than the whole potion stock"),
		[47]=(70f,"50% chance of Blink on a shield block, and Blink nullifies a physical hit outright"),
		[57]=(60f,"two items on every treasure space, which is the one effect that compounds over a board"),
		[28]=(55f,"20% chance of Blink whenever you are hit"),
		[30]=(45f,"360 potency back every time you take a physical hit"),
		[15]=(40f,"absorbs earth damage, and Stoneskin worth 15% of maximum HP on a block"),
		[38]=(50f,"240 extra potency on every auto-attack"),
		[39]=(50f,"240 extra potency on every auto-attack"),
		[40]=(50f,"240 extra potency on every auto-attack"),
		[41]=(50f,"240 extra potency on every auto-attack"),
		[42]=(50f,"240 extra potency on every auto-attack"),
		[43]=(50f,"240 extra potency on every auto-attack"),
		[4]=(15f,"one random beast potion per campsite, and campsites are rare"),
		[2]=(10f,"revives you by sacrificing the familiar, which is the thing doing the tanking")
	};
	public static float Of(CrucibleItem item) {
		if(!Merits.TryGetValue(item.Id,out (float,string) value)) {
			return 0f;
		}
		return value.Item1;
	}
	public static string Reason(CrucibleItem item) {
		if(!Merits.TryGetValue(item.Id,out (float,string) value)) {
			return string.Empty;
		}
		return value.Item2;
	}
	public static bool IsElementalAxe(CrucibleItem item) {
		int id=item.Id;
		if(id>=38) {
			return id<=43;
		}
		return false;
	}
}
