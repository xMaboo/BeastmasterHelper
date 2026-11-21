using System;
namespace Beastmaster;

public sealed record CrucibleGuideFight(string Name,string Kind,string[] Match,int[] Team,string[] Notes,string[] Wants) {
	public string Why { get; init; }=string.Empty;
	public BeastKin[] BorrowKins { get; init; }=Array.Empty<BeastKin>();
	public string[] Busters { get; init; }=Array.Empty<string>();
	public string[] Interrupts { get; init; }=Array.Empty<string>();
	public string[] Dispels { get; init; }=Array.Empty<string>();
	public string[] Abilities { get; init; }=Array.Empty<string>();
	public BeastKin OpenWith { get; init; }
	public string[] TemperedFor { get; init; }=Array.Empty<string>();
}
