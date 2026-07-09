using WrathCombo.Services;
using Xunit;

namespace WrathCombo.Tests;

public class HealDelayCurveTests
{
    [Theory]
    [InlineData(1.5, 100f)]
    [InlineData(1.5, 50f)]
    [InlineData(1.5, 0f)]
    [InlineData(0.2, 62.5f)]
    public void CurveDisabled_ReturnsRawBaseDelay_Uncapped(double baseHealDelay, float lowestHp)
    {
        var result = HealDelayCurve.ComputeEffectiveHealDelay(baseHealDelay, lowestHp, dynamicCurveEnabled: false);

        Assert.Equal(baseHealDelay, result);
    }

    [Fact]
    public void CurveEnabled_AtOrBelowCriticalThreshold_ReturnsInstant()
    {
        var result = HealDelayCurve.ComputeEffectiveHealDelay(baseHealDelay: 1.5, lowestHp: 50f, dynamicCurveEnabled: true);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CurveEnabled_BelowCriticalThreshold_ReturnsInstant()
    {
        var result = HealDelayCurve.ComputeEffectiveHealDelay(baseHealDelay: 1.5, lowestHp: 10f, dynamicCurveEnabled: true);

        Assert.Equal(0.0, result);
    }

    [Theory]
    [InlineData(1.5, 75f)]
    [InlineData(1.5, 100f)]
    [InlineData(3.0, 90f)]
    [InlineData(0.2, 100f)]
    public void CurveEnabled_AtOrAboveHealthyThreshold_ReturnsFullConfiguredDelay(double baseHealDelay, float lowestHp)
    {
        // The user's configured delay must apply IN FULL when heal targets are healthy —
        // no hidden cap. The retired 500ms experimental clamp overrode user settings and
        // caused twitch-healing on chip damage (2026-07-06 incident); this test pins the
        // fix so a future "optimization" cannot silently reintroduce a cap.
        var result = HealDelayCurve.ComputeEffectiveHealDelay(baseHealDelay, lowestHp, dynamicCurveEnabled: true);

        Assert.Equal(baseHealDelay, result);
    }

    [Fact]
    public void CurveEnabled_MidpointBetweenThresholds_ReturnsHalfOfConfiguredDelay()
    {
        // 62.5 is exactly halfway between 50 and 75 -> t=0.5 -> half of the configured delay.
        var result = HealDelayCurve.ComputeEffectiveHealDelay(baseHealDelay: 1.5, lowestHp: 62.5f, dynamicCurveEnabled: true);

        Assert.Equal(0.75, result, precision: 5);
    }

    [Theory]
    [InlineData(55f, 0.3)]
    [InlineData(70f, 1.2)]
    public void CurveEnabled_WithinLinearRange_InterpolatesProportionally(float lowestHp, double expected)
    {
        // t = (hp-50)/25. result = t * baseHealDelay (1.5).
        var result = HealDelayCurve.ComputeEffectiveHealDelay(baseHealDelay: 1.5, lowestHp, dynamicCurveEnabled: true);

        Assert.Equal(expected, result, precision: 5);
    }
}
