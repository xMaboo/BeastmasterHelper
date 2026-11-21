using System.Collections.Generic;
using System.Numerics;
namespace Beastmaster;

public sealed record CruciblePlatform(Vector3 Centre,CruciblePlatformKind Kind,string Label,int Step) {
	public CruciblePlatformKind? Leads { get; init; }
	public IReadOnlyList<string>? Opens { get; init; }
	public const float Radius=2.5f;
	public const float ArrivalRadius=1f;
	public bool AtCentre(Vector3 position) {
		return Vector2.Distance(new Vector2(position.X,position.Z),new Vector2(Centre.X,Centre.Z))<=1f;
	}
	public bool Contains(Vector3 position) {
		return Vector2.Distance(new Vector2(position.X,position.Z),new Vector2(Centre.X,Centre.Z))<=2.5f;
	}
}
