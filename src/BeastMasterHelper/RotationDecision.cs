namespace Beastmaster;

public readonly record struct RotationDecision(uint ActionId,string Reason,string Phase) {
	public bool HasAction => ActionId!=0;
	public static readonly RotationDecision None=new RotationDecision(0u,"Nothing to do","Idle");
}
