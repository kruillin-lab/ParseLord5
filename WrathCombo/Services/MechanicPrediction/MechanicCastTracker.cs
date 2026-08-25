using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using WrathCombo.Combos.PvE;
using WrathCombo.CustomComboNS.Functions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;
using static WrathCombo.Data.ActionWatching;
namespace WrathCombo.Services.MechanicPrediction;

/// <summary>
///     Dalamud-facing scanner that turns the hostile-caster cast bar into a
///     forward-looking spike prediction. Mirrors <see cref="RaidwideCasting"/>
///     for hostile-caster iteration and reuses
///     <see cref="HasIncomingTankBusterEffect(System.Nullable{IGameObject})"/>
///     for the VFX telegraph. Inert unless
///     <see cref="Service.Configuration.PredictiveMechanics"/> is on.
/// </summary>
internal static class MechanicCastTracker
{
    private static MechanicCastPrediction _last;

    internal static MechanicCastPrediction Last => _last;

    internal static void Update()
    {
        _last = default;
        if (!Service.Configuration.PredictiveMechanics)
            return;

        MechanicCastPrediction? best = null;

        foreach (var obj in Svc.Objects)
        {
            if (obj is not IBattleChara caster || !caster.IsHostile() || !caster.IsCasting)
                continue;

            if (!ActionSheet.TryGetValue(caster.CastActionId, out var spellSheet))
                continue;

            var prediction = MechanicCastClassifier.Classify(
                isHostileCasting: true,
                castType: spellSheet.CastType,
                effectRange: spellSheet.EffectRange,
                castTargetObjectId: caster.CastTargetObjectId,
                localPlayerObjectId: LocalPlayer?.GameObjectId ?? 0,
                hasTankbusterVfx: HasIncomingTankBusterEffect(),
                totalCastTime: caster.TotalCastTime,
                currentCastTime: caster.CurrentCastTime,
                castActionId: caster.CastActionId);

            if (prediction.Kind == MechanicCastKind.None)
                continue;

            if (best is not { } current ||
                prediction.TimeToImpactSeconds < current.TimeToImpactSeconds ||
                (prediction.TimeToImpactSeconds == current.TimeToImpactSeconds &&
                 Priority(prediction.Kind) > Priority(current.Kind)))
            {
                best = prediction;
            }
        }

        _last = best ?? default;
    }

    internal static float PredictedSpikeFraction() => _last.PredictedSpikeFraction;

    internal static bool HasImminentImpact(float withinSeconds) =>
        _last.Kind != MechanicCastKind.None && _last.TimeToImpactSeconds <= withinSeconds;

    /// <summary>
    ///     Kind-scoped imminence check, for consumers that must react to one
    ///     mechanic class only (e.g. party-wide heals should not fire on a
    ///     single-target <see cref="MechanicCastKind.Tankbuster" />).
    /// </summary>
    internal static bool HasImminentImpact(MechanicCastKind kind, float withinSeconds) =>
        _last.Kind == kind && _last.TimeToImpactSeconds <= withinSeconds;

    private static int Priority(MechanicCastKind kind) => kind switch
    {
        MechanicCastKind.Tankbuster => 3,
        MechanicCastKind.Raidwide => 2,
        MechanicCastKind.Cleave => 1,
        _ => 0,
    };
}
