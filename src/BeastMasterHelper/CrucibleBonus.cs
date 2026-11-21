namespace Beastmaster;

public readonly record struct CrucibleBonus(string Stat,float Percent) {
	public override string ToString() {
		return $"{Stat} {((Percent>=0f)?"+":string.Empty)}{Percent:0.#}%";
	}
}
