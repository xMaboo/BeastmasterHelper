namespace Beastmaster;

public readonly record struct CaptureVerdict(bool ShouldCapture,string Reason) {
	public static readonly CaptureVerdict Yes=new CaptureVerdict(ShouldCapture: true,string.Empty);
	public static CaptureVerdict No(string reason) {
		return new CaptureVerdict(ShouldCapture: false,reason);
	}
}
