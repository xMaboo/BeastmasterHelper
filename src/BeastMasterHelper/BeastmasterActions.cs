using System.Collections.Generic;
namespace Beastmaster;

public static class BeastmasterActions {
	public const uint ClassJobId=43u;
	public const uint SmashAxe=44879u;
	public const uint AxebladeBite=44883u;
	public const uint Shieldsplitter=44885u;
	public const uint AvalancheAxe=44884u;
	public const uint MistralAxe=44887u;
	public const uint SpinningAxe=44888u;
	public const uint GaleAxe=44889u;
	public const uint Trick=47093u;
	public const uint PartingBlow=44891u;
	public const uint TemperedRelease=44890u;
	public const uint Borrow=44895u;
	public const uint BeastMode=44886u;
	public const uint FirstBattlehorn=44881u;
	public const uint SecondBattlehorn=44892u;
	public const uint ThirdBattlehorn=44894u;
	public const uint ShieldCharge=44893u;
	public const uint Rally=44905u;
	public const uint RallyingCheer=44904u;
	public const uint Capture=44880u;
	public const uint Sprint=3u;
	public const uint PotionSlot=900001u;
	public const uint VolantHeart=4595u;
	public const uint RampantHeart=4596u;
	public const uint DurantHeart=4597u;
	public const uint EldritchHeart=4598u;
	public const uint Sunstrider=4599u;
	public const uint Moonstalker=4600u;
	public const uint OneWithNature=4601u;
	public const uint BeastKinship=4602u;
	public const uint VileKinship=4603u;
	public const uint CloudKinship=4604u;
	public const uint SeedKinship=4605u;
	public const uint WaveKinship=4606u;
	public const uint ScaleKinship=4607u;
	public const uint SoulKinship=4608u;
	public const uint AshKinship=4609u;
	private const uint KinshipAltOffset=42u;
	public const uint LingeringVantage=4614u;
	public const uint WaveringHeart=4643u;
	public const uint CapturingInterest=4624u;
	public const uint InterestCaptured=4626u;
	private static readonly IReadOnlyDictionary<uint,int> UnlockLevels=new Dictionary<uint,int> {
		[44879u]=1,
		[44880u]=1,
		[44881u]=1,
		[44883u]=2,
		[44884u]=4,
		[44891u]=6,
		[44887u]=8,
		[47093u]=8,
		[44892u]=10,
		[44885u]=12,
		[44888u]=14,
		[44889u]=16,
		[44890u]=18,
		[44894u]=20,
		[44886u]=22,
		[44895u]=22,
		[44893u]=24,
		[44905u]=28,
		[44904u]=40
	};
	public static readonly uint[] HeartStatuses=new uint[4] { 4595u, 4596u, 4597u, 4598u };
	public static readonly uint[] Battlehorns=new uint[3] { 44881u, 44892u, 44894u };
	private static readonly HashSet<uint> SelfTargetedActions=new HashSet<uint> { 44881u, 44892u, 44894u, 44905u, 44904u, 44890u, 44895u, 44886u, 3u };
	public const uint BorrowFirstVariant=47238u;
	public const uint FirstKinshipAction=44896u;
	public static int UnlockLevel(uint actionId) {
		if(!UnlockLevels.TryGetValue(actionId,out var value)) {
			return 1;
		}
		return value;
	}
	public static bool IsUnlocked(uint actionId,int playerLevel) {
		return playerLevel>=UnlockLevel(actionId);
	}
	public static uint AxeFor(InstinctAffinity affinity) {
		return affinity switch {
			InstinctAffinity.Rampant => 44884u,
			InstinctAffinity.Durant => 44887u,
			InstinctAffinity.Eldritch => 44888u,
			InstinctAffinity.Volant => 44889u,
			_ => 0u,
		};
	}
	public static InstinctAffinity AffinityOf(uint axeActionId) {
		return axeActionId switch {
			44884u => InstinctAffinity.Rampant,
			44887u => InstinctAffinity.Durant,
			44888u => InstinctAffinity.Eldritch,
			44889u => InstinctAffinity.Volant,
			_ => InstinctAffinity.None,
		};
	}
	public static uint HeartFor(InstinctAffinity affinity) {
		return affinity switch {
			InstinctAffinity.Volant => 4595u,
			InstinctAffinity.Rampant => 4596u,
			InstinctAffinity.Durant => 4597u,
			InstinctAffinity.Eldritch => 4598u,
			_ => 0u,
		};
	}
	public static InstinctAffinity AffinityOfHeart(uint statusId) {
		return statusId switch {
			4595u => InstinctAffinity.Volant,
			4596u => InstinctAffinity.Rampant,
			4597u => InstinctAffinity.Durant,
			4598u => InstinctAffinity.Eldritch,
			_ => InstinctAffinity.None,
		};
	}
	public static InstinctAffinity NextInWheel(InstinctAffinity heldHeart) {
		return heldHeart switch {
			InstinctAffinity.Volant => InstinctAffinity.Rampant,
			InstinctAffinity.Rampant => InstinctAffinity.Durant,
			InstinctAffinity.Durant => InstinctAffinity.Eldritch,
			InstinctAffinity.Eldritch => InstinctAffinity.Volant,
			_ => InstinctAffinity.None,
		};
	}
	public static uint IntentionalHalf(InstinctAffinity completingAffinity) {
		return completingAffinity switch {
			InstinctAffinity.Rampant => 4599u,
			InstinctAffinity.Eldritch => 4599u,
			InstinctAffinity.Durant => 4600u,
			InstinctAffinity.Volant => 4600u,
			_ => 0u,
		};
	}
	public static BeastKin KinshipOfStatus(uint statusId) {
		return ((statusId>=4644)?(statusId-42):statusId) switch {
			4602u => BeastKin.Beastkin,
			4603u => BeastKin.Vilekin,
			4604u => BeastKin.Cloudkin,
			4605u => BeastKin.Seedkin,
			4606u => BeastKin.Wavekin,
			4607u => BeastKin.Scalekin,
			4608u => BeastKin.Soulkin,
			4609u => BeastKin.Ashkin,
			_ => BeastKin.Unknown,
		};
	}
	public static uint OppositeHalf(uint half) {
		return half switch {
			4600u => 4599u,
			4599u => 4600u,
			_ => 0u,
		};
	}
	public static uint FinalComboStep(int playerLevel) {
		if(playerLevel<UnlockLevel(44885u)) {
			if(playerLevel<UnlockLevel(44883u)) {
				return 44879u;
			}
			return 44883u;
		}
		return 44885u;
	}
	public static bool IsSelfTargeted(uint actionId) {
		return SelfTargetedActions.Contains(actionId);
	}
	public static uint BorrowFor(BeastKin kin) {
		if(kin!=BeastKin.Unknown) {
			return (uint)(47238+kin-1);
		}
		return 44895u;
	}
	public static uint KinshipActionFor(BeastKin kin) {
		if(kin!=BeastKin.Unknown) {
			return (uint)(44896+kin-1);
		}
		return 44886u;
	}
	public static bool HasCastTime(uint actionId) {
		if(actionId!=44881&&actionId!=44892) {
			return actionId==44894;
		}
		return true;
	}
}
