using System.Numerics;
namespace Beastmaster;

public static class RotationPanels {
	public const string Core="core";
	public const string Rotation="rotation";
	public const string Familiar="familiar";
	public const string Thresholds="thresholds";
	public const string Hotbar="hotbar";
	public static readonly (string Key,string Title)[] All=new (string,string)[4] {
		("rotation","Rotation"),
		("familiar","Familiar"),
		("thresholds","Thresholds"),
		("hotbar","Actions")
	};
	public static string TitleOf(string key) {
		(string,string)[] all=All;
		for(int i=0;i<all.Length;i++) {
			var (text,result)=all[i];
			if(text==key) {
				return result;
			}
		}
		return key;
	}
	public static OverlayPanelSettings Defaults(string key) {
		return key switch {
			"core" => new OverlayPanelSettings {
				Width = 300f,
				Columns = 3,
				PositionX = 20f,
				PositionY = 420f
			},
			"rotation" => new OverlayPanelSettings {
				Width = 300f,
				Columns = 3,
				PositionX = 20f,
				PositionY = 510f
			},
			"familiar" => new OverlayPanelSettings {
				Width = 300f,
				Columns = 3,
				PositionX = 20f,
				PositionY = 650f
			},
			"thresholds" => new OverlayPanelSettings {
				Width = 300f,
				Columns = 3,
				PositionX = 330f,
				PositionY = 420f
			},
			"hotbar" => new OverlayPanelSettings {
				Width = 340f,
				Columns = 3,
				PositionX = 330f,
				PositionY = 560f
			},
			_ => new OverlayPanelSettings(),
		};
	}
	public static Vector2 DefaultPosition(string key) {
		OverlayPanelSettings overlayPanelSettings=Defaults(key);
		return new Vector2(overlayPanelSettings.PositionX,overlayPanelSettings.PositionY);
	}
}
