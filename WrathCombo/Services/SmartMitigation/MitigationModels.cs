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
    bool InvulnActive,
    bool LongMitigationActive = false,
    bool ShortMitigationActive = false);

internal readonly record struct MitigationCoverageRequest(
    uint CurrentHp,
    uint MaxHp,
    float IncomingDps,
    float IncomingHps,
    float MechanicSpikeFraction,
    float HorizonSeconds,
    float SafetyHpPercent,
    bool ConfirmedTankbuster = false,
    float SustainMultiplier = 1f,
    /// <summary>Prefer Large (Damnation) over Small when multiple CDs satisfy coverage; only set when ShouldOfferDamnation is true.</summary>
    bool PreferHeavyMitigation = false);

internal readonly record struct PlayerPressureState(
    float IncomingDps,
    float IncomingHps,
    float NetDps,
    float DangerRatio,
    float MaxSingleHit,
    float? SecondsUntilDeath,
    int? TankCooldownDangerLevel = null)
{
    public bool FromTankCooldownHelper => TankCooldownDangerLevel is not null;

    /// <summary>TCH danger level Warning (1) or higher.</summary>
    public bool TankCooldownInDanger => TankCooldownDangerLevel is >= 1;

    /// <summary>TCH danger level Critical (2) or higher.</summary>
    public bool TankCooldownCritical => TankCooldownDangerLevel is >= 2;

    /// <summary>TCH danger level Emergency (3).</summary>
    public bool TankCooldownEmergency => TankCooldownDangerLevel is >= 3;

    /// <summary>
    ///     TankCooldownHelper's IPC payload carries no max-single-hit field, so pressure from that
    ///     source always arrives with <see cref="MaxSingleHit"/> at 0. Backfill it from local
    ///     HP-delta telemetry, which runs every frame regardless of which source supplied the rest.
    ///     Without this, every gate that requires a landed hit is dead whenever TCH is installed.
    /// </summary>
    public PlayerPressureState WithLocalMaxSingleHit(float localMaxSingleHit) =>
        MaxSingleHit > 0f ? this : this with { MaxSingleHit = localMaxSingleHit };
}

internal readonly record struct MitigationCoverageResult(
    uint ActionId,
    float RequiredReduction,
    float ActiveReduction,
    float IncomingDamageBudget,
    string Reason);
