using System;
namespace Beastmaster;

public sealed record FamiliarInfo(int Number,string Name,BeastKin Kin,string AutoAttack,FamiliarAbility Tempered,FamiliarAbility Trick,string Borrow) {
	public bool TemperedLeaves => Tempered.Effect.Contains("retreats",StringComparison.OrdinalIgnoreCase);
}
