using System;
namespace Beastmaster;

public sealed class OverlayPanelSettings {
	public bool Show { get; set; }=true;
	public bool Locked { get; set; }
	public float Width { get; set; }=320f;
	public int Columns { get; set; }=3;
	public float PositionX { get; set; }
	public float PositionY { get; set; }
	public bool HasPosition {
		get {
			if(PositionX==0f) {
				return PositionY!=0f;
			}
			return true;
		}
	}
	public void Clamp() {
		Width=Math.Clamp(Width,140f,900f);
		Columns=Math.Clamp(Columns,1,6);
	}
}
