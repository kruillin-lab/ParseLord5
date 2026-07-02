using System;

namespace WrathCombo.Services;

/// <summary>ParseLord5: dynamic linear reaction delay scaled by lowest HP% among heal targets.</summary>
internal static class HealDelayCurve
{
    internal const float CriticalHpThreshold = 50f;
    internal const float HealthyHpThreshold = 75f;
    internal const double ExperimentalMaxDelaySeconds = 0.5;

    internal static double ComputeEffectiveHealDelay(double baseHealDelay, float lowestHp, bool dynamicCurveEnabled)
    {
        if (!dynamicCurveEnabled)
            return baseHealDelay;

        var maxDelay = Math.Min(baseHealDelay, ExperimentalMaxDelaySeconds);

        if (lowestHp <= CriticalHpThreshold)
            return 0.0;

        if (lowestHp >= HealthyHpThreshold)
            return maxDelay;

        var t = (lowestHp - CriticalHpThreshold) / (HealthyHpThreshold - CriticalHpThreshold);
        return t * maxDelay;
    }
}
