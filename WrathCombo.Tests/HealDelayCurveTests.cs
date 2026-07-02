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
        // With the experiment off, the curve must not apply the 500ms cap — this is the
        // asymmetry a careless extraction could collapse (capping in both flag states).
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

    [Fact]
    public void CurveEnabled_AtOrAboveHealthyThreshold_ReturnsCappedMaxDelay()
    {
        // baseHealDelay (1.5s) exceeds the 500ms experimental cap, so the result must be capped.
        var result = HealDelayCurve.ComputeEffectiveHealDelay(baseHealDelay: 1.5, lowestHp: 75f, dynamicCurveEnabled: true);

        Assert.Equal(0.5, result);
    }

    [Fact]
    public void CurveEnabled_AboveHealthyThreshold_ReturnsCappedMaxDelay()
    {
        var result = HealDelayCurve.ComputeEffectiveHealDelay(baseHealDelay: 1.5, lowestHp: 100f, dynamicCurveEnabled: true);

        Assert.Equal(0.5, result);
    }

    [Fact]
    public void CurveEnabled_BaseDelayBelowCap_HealthyThresholdUsesUncappedBase()
    {
        // maxDelay = Min(baseHealDelay, 0.5) — when base is already under the cap, the healthy
        // plateau should equal the base delay itself, not the 500ms constant.
        var result = HealDelayCurve.ComputeEffectiveHealDelay(baseHealDelay: 0.2, lowestHp: 100f, dynamicCurveEnabled: true);

        Assert.Equal(0.2, result);
    }

    [Fact]
    public void CurveEnabled_MidpointBetweenThresholds_ReturnsHalfOfMaxDelay()
    {
        // 62.5 is exactly halfway between 50 and 75 -> t=0.5 -> half of the capped max delay.
        var result = HealDelayCurve.ComputeEffectiveHealDelay(baseHealDelay: 1.5, lowestHp: 62.5f, dynamicCurveEnabled: true);

        Assert.Equal(0.25, result, precision: 5);
    }

    [Theory]
    [InlineData(55f, 0.1)]
    [InlineData(70f, 0.4)]
    public void CurveEnabled_WithinLinearRange_InterpolatesProportionally(float lowestHp, double expected)
    {
        // maxDelay = 0.5 (base 1.5 > cap). t = (hp-50)/25. result = t * 0.5.
        var result = HealDelayCurve.ComputeEffectiveHealDelay(baseHealDelay: 1.5, lowestHp, dynamicCurveEnabled: true);

        Assert.Equal(expected, result, precision: 5);
    }
}
