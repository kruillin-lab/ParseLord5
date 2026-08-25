using WrathCombo.Services.MechanicPrediction;
using Xunit;

namespace WrathCombo.Tests;

public class MechanicCastClassifierTests
{
    private const ulong Player = 1000UL;
    private const ulong Other = 2000UL;

    [Fact]
    public void NotCasting_ReturnsNone()
    {
        var result = MechanicCastClassifier.Classify(
            isHostileCasting: false, castType: 2, effectRange: 40f,
            castTargetObjectId: Player, localPlayerObjectId: Player,
            hasTankbusterVfx: true, totalCastTime: 3f, currentCastTime: 0f,
            castActionId: 1234);

        Assert.Equal(MechanicCastKind.None, result.Kind);
        Assert.Equal(0f, result.PredictedSpikeFraction);
    }

    [Fact]
    public void CastBeyondMaxLead_ReturnsNone()
    {
        var result = MechanicCastClassifier.Classify(
            isHostileCasting: true, castType: 2, effectRange: 40f,
            castTargetObjectId: Player, localPlayerObjectId: Player,
            hasTankbusterVfx: true, totalCastTime: 10f, currentCastTime: 0f,
            castActionId: 1234);

        Assert.Equal(MechanicCastKind.None, result.Kind);
    }

    [Fact]
    public void RaidwideCast_ReturnsRaidwideSpike()
    {
        var result = MechanicCastClassifier.Classify(
            isHostileCasting: true, castType: 2, effectRange: 40f,
            castTargetObjectId: Other, localPlayerObjectId: Player,
            hasTankbusterVfx: false, totalCastTime: 3f, currentCastTime: 1f,
            castActionId: 5678);

        Assert.Equal(MechanicCastKind.Raidwide, result.Kind);
        Assert.Equal(MechanicCastClassifier.RaidwideSpikeFraction, result.PredictedSpikeFraction);
        Assert.Equal(2f, result.TimeToImpactSeconds, precision: 5);
        Assert.Equal(5678u, result.CastActionId);
    }

    [Fact]
    public void PlayerTargetedCastWithVfx_ReturnsTankbuster()
    {
        var result = MechanicCastClassifier.Classify(
            isHostileCasting: true, castType: 0, effectRange: 0f,
            castTargetObjectId: Player, localPlayerObjectId: Player,
            hasTankbusterVfx: true, totalCastTime: 2f, currentCastTime: 0.5f,
            castActionId: 999);

        Assert.Equal(MechanicCastKind.Tankbuster, result.Kind);
        Assert.Equal(MechanicCastClassifier.TankbusterSpikeFraction, result.PredictedSpikeFraction);
        Assert.Equal(1.5f, result.TimeToImpactSeconds, precision: 5);
    }

    [Fact]
    public void PlayerTargetedCastWithoutVfx_ReturnsCleave()
    {
        var result = MechanicCastClassifier.Classify(
            isHostileCasting: true, castType: 0, effectRange: 0f,
            castTargetObjectId: Player, localPlayerObjectId: Player,
            hasTankbusterVfx: false, totalCastTime: 2f, currentCastTime: 1f,
            castActionId: 111);

        Assert.Equal(MechanicCastKind.Cleave, result.Kind);
        Assert.Equal(MechanicCastClassifier.CleaveSpikeFraction, result.PredictedSpikeFraction);
    }

    [Fact]
    public void NegativeRemainingTime_ClampsToZero()
    {
        var result = MechanicCastClassifier.Classify(
            isHostileCasting: true, castType: 2, effectRange: 40f,
            castTargetObjectId: Other, localPlayerObjectId: Player,
            hasTankbusterVfx: false, totalCastTime: 1f, currentCastTime: 2f,
            castActionId: 1);

        Assert.Equal(MechanicCastKind.Raidwide, result.Kind);
        Assert.Equal(0f, result.TimeToImpactSeconds);
    }
}
