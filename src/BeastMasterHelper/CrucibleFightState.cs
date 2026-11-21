using System.Collections.Generic;
namespace Beastmaster;

public sealed record CrucibleFightState(string Enemy,string Weakness,string[] WeaknessTypes,IReadOnlyList<CrucibleFamiliar> Familiars,string Error) {
	public bool Valid => Error.Length==0;
}
