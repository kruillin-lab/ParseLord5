namespace WrathCombo.Services.SmartMitigation;

internal enum MitigationTier
{
    Small = 0,
    Medium = 1,
    Large = 2,
    MaxHpBoost = 3,
    Invuln = 4,
}

internal readonly record struct MitigationOption(
    uint ActionId,
    float DamageReduction,
    float ShieldPotency,
    float MaxHpBonusFraction,
    float CooldownSeconds,
    MitigationTier Tier);

internal readonly record struct ActiveMitigationState(
    float CombinedDamageReduction,
    float ActiveShield,
    float ActiveMaxHpBonusFraction,
    bool InvulnActive);

internal readonly record struct MitigationCoverageRequest(
    uint CurrentHp,
    uint MaxHp,
    float IncomingDps,
    float IncomingHps,
    float MechanicSpikeFraction,
    float HorizonSeconds,
    float SafetyHpPercent,
    bool ConfirmedTankbuster = false,
    float SustainMultiplier = 1f);

internal readonly record struct PlayerPressureState(
    float IncomingDps,
    float IncomingHps,
    float NetDps,
    float DangerRatio,
    float MaxSingleHit,
    float? SecondsUntilDeath);

internal readonly record struct MitigationCoverageResult(
    uint ActionId,
    float RequiredReduction,
    float ActiveReduction,
    float IncomingDamageBudget,
    string Reason);
