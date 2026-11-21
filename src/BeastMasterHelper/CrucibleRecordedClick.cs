using System;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

public sealed class CrucibleRecordedClick {
	public required string Addon { get; init; }
	public required uint EventType { get; init; }
	public required int Param { get; init; }
	public required uint TargetNodeId { get; init; }
	public required string RowText { get; init; }
	public required byte[] Payload { get; init; }
	public DateTime AtUtc { get; init; }=DateTime.UtcNow;
	public override string ToString() {
		return $"{AtUtc:HH:mm:ss.fff} {Addon} type={(AtkEventType)(byte)EventType} ({EventType}) param={Param} node={TargetNodeId} row=\"{RowText}\" payload={Convert.ToHexString(Payload)}";
	}
}
