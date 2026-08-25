using System;

namespace WrathCombo.Services.MechanicPrediction;

internal enum MechanicCastKind
{
    None,
    Raidwide,
    Tankbuster,
    Cleave,
}

internal readonly record struct MechanicCastPrediction(
    MechanicCastKind Kind,
    float TimeToImpactSeconds,
    float PredictedSpikeFraction,
    uint CastActionId);

/// <summary>
///     Pure classification of an in-progress hostile cast into a forward-looking
///     spike prediction. No plugin-framework imports so it can be compiled into
///     <c>WrathCombo.Tests</c> via the test project's source-link mechanism.
/// </summary>
internal static class MechanicCastClassifier
{
    internal const float RaidwideSpikeFraction = 0.20f;
    internal const float TankbusterSpikeFraction = 0.45f;
    internal const float CleaveSpikeFraction = 0.20f;
    internal const float MaxLeadSeconds = 6f;

    internal static MechanicCastPrediction Classify(
        bool isHostileCasting,
        byte castType,
        float effectRange,
        ulong castTargetObjectId,
        ulong localPlayerObjectId,
        bool hasTankbusterVfx,
        float totalCastTime,
        float currentCastTime,
        uint castActionId)
    {
        if (!isHostileCasting)
            return default;

        var timeToImpact = Math.Max(0f, totalCastTime - currentCastTime);
        if (timeToImpact > MaxLeadSeconds)
            return default;

        // Mirrors RaidwideCasting's filter exactly (Action.cs): cast type 2 or 5
        // with a wide effect range is a raidwide.
        var isRaidwide = castType is 2 or 5 && effectRange >= 30f;
        if (isRaidwide)
            return new MechanicCastPrediction(
                MechanicCastKind.Raidwide, timeToImpact, RaidwideSpikeFraction, castActionId);

        var targetsPlayer = castTargetObjectId == localPlayerObjectId;
        if (!targetsPlayer)
            return default;

        if (hasTankbusterVfx)
            return new MechanicCastPrediction(
                MechanicCastKind.Tankbuster, timeToImpact, TankbusterSpikeFraction, castActionId);

        return new MechanicCastPrediction(
            MechanicCastKind.Cleave, timeToImpact, CleaveSpikeFraction, castActionId);
    }
}
