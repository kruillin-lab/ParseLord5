using System;

namespace WrathCombo.Services;

/// <summary>
///     ParseLord5: dynamic linear reaction delay scaled by lowest HP% among heal targets.
///     The configured delay is the healthy-state baseline and is NEVER clamped — chip
///     damage above <see cref="HealthyHpThreshold"/> waits the full configured delay
///     (and is typically out-regenerated before it elapses, producing no cast at all).
///     The curve only ACCELERATES reaction as HP drops: linear from the configured
///     delay at <see cref="HealthyHpThreshold"/> down to instant at
///     <see cref="CriticalHpThreshold"/>. (The retired experimental 500ms global cap
///     made the plugin twitch-heal every scrap of damage regardless of user settings —
///     see wargames/parselord5-stability-battle-plan.md, 2026-07-06 incidents.)
/// </summary>
internal static class HealDelayCurve
{
    internal const float CriticalHpThreshold = 50f;
    internal const float HealthyHpThreshold = 75f;

    internal static double ComputeEffectiveHealDelay(double baseHealDelay, float lowestHp, bool dynamicCurveEnabled)
    {
        if (!dynamicCurveEnabled)
            return baseHealDelay;

        if (lowestHp <= CriticalHpThreshold)
            return 0.0;

        if (lowestHp >= HealthyHpThreshold)
            return baseHealDelay;

        var t = (lowestHp - CriticalHpThreshold) / (HealthyHpThreshold - CriticalHpThreshold);
        return t * baseHealDelay;
    }
}
