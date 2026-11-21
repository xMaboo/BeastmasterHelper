using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
namespace Beastmaster;

public static class ArenaOverlayRenderer {
	private const int CircleSegments=72;
	public static void Draw(BeastmasterConfiguration configuration) {
		if(!configuration.ArenaOverlayEnabled) {
			return;
		}
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer!=null) {
			Vector3 centre=((configuration.ArenaCentreMode==ArenaCentreMode.Player)?localPlayer.Position:new Vector3(configuration.ArenaCentreX,configuration.ArenaCentreY,configuration.ArenaCentreZ));
			float num=Math.Max(1f,configuration.ArenaSize);
			uint colour=ImGui.ColorConvertFloat4ToU32(new Vector4(configuration.ArenaColourR,configuration.ArenaColourG,configuration.ArenaColourB,configuration.ArenaColourA));
			StrokeClosed(configuration.ArenaShape switch {
				ArenaShape.Circle => CirclePoints(centre,num),
				ArenaShape.Rectangle => RectanglePoints(centre,num,Math.Max(1f,configuration.ArenaSizeZ),configuration.ArenaRotationDegrees),
				_ => RectanglePoints(centre,num,num,configuration.ArenaRotationDegrees),
			},colour,configuration.ArenaThickness);
			if(configuration.ArenaShowCentre) {
				DrawCentreMark(centre,colour);
			}
		}
	}
	private static IEnumerable<Vector3> CirclePoints(Vector3 centre,float radius) {
		for(int i=0;i<72;i++) {
			float x=(float)i*((float)Math.PI*2f)/72f;
			Vector3 vector=centre;
			vector.X=centre.X+MathF.Cos(x)*radius;
			vector.Z=centre.Z+MathF.Sin(x)*radius;
			yield return vector;
		}
	}
	private static IEnumerable<Vector3> RectanglePoints(Vector3 centre,float halfX,float halfZ,float rotationDegrees) {
		float x=rotationDegrees*(float)Math.PI/180f;
		float cos=MathF.Cos(x);
		float sin=MathF.Sin(x);
		Vector2[] array=new Vector2[4] {
			new Vector2(0f-halfX,0f-halfZ),
			new Vector2(halfX,0f-halfZ),
			new Vector2(halfX,halfZ),
			new Vector2(0f-halfX,halfZ)
		};
		Vector2[] array2=array;
		for(int i=0;i<array2.Length;i++) {
			Vector2 vector=array2[i];
			float num=vector.X*cos-vector.Y*sin;
			float num2=vector.X*sin+vector.Y*cos;
			Vector3 vector2=centre;
			vector2.X=centre.X+num;
			vector2.Z=centre.Z+num2;
			yield return vector2;
		}
	}
	private static void StrokeClosed(IEnumerable<Vector3> worldPoints,uint colour,float thickness) {
		List<Vector3> list=worldPoints.ToList();
		if(list.Count<2) {
			return;
		}
		ImDrawListPtr foregroundDrawList=ImGui.GetForegroundDrawList();
		for(int i=0;i<list.Count;i++) {
			Vector3 worldPos=list[i];
			Vector3 worldPos2=list[(i+1)%list.Count];
			if(DalamudApi.GameGui.WorldToScreen(worldPos,out var screenPos,out var inView)&&inView&&DalamudApi.GameGui.WorldToScreen(worldPos2,out var screenPos2,out var inView2)&&inView2) {
				foregroundDrawList.AddLine(screenPos,screenPos2,colour,thickness);
			}
		}
	}
	private static void DrawCentreMark(Vector3 centre,uint colour) {
		ImDrawListPtr foregroundDrawList=ImGui.GetForegroundDrawList();
		(Vector3,Vector3)[] array=new (Vector3,Vector3)[2];
		Vector3 vector=centre;
		vector.X=centre.X-1.5f;
		Vector3 item=vector;
		vector=centre;
		vector.X=centre.X+1.5f;
		array[0]=(item,vector);
		vector=centre;
		vector.Z=centre.Z-1.5f;
		Vector3 item2=vector;
		vector=centre;
		vector.Z=centre.Z+1.5f;
		array[1]=(item2,vector);
		(Vector3,Vector3)[] array2=array;
		Vector2 screenPos2=default;
		bool inView2=default;
		for(int i=0;i<array2.Length;i++) {
			var (worldPos,worldPos2)=array2[i];
			if(((DalamudApi.GameGui.WorldToScreen(worldPos,out var screenPos,out var inView)&inView)&&DalamudApi.GameGui.WorldToScreen(worldPos2,out screenPos2,out inView2))&inView2) {
				foregroundDrawList.AddLine(screenPos,screenPos2,colour,2f);
			}
		}
	}
}
