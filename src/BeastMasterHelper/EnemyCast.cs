namespace Beastmaster;

public sealed record EnemyCast(string Name,DamageKind Kind,bool IsBuster,bool IsRaidwide=false,float Remaining=0f);
