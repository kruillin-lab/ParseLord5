using System;
using System.Collections.Generic;

namespace WrathCombo.Services.SmartMitigation;

internal static class MitigationCoverageCalculator
{
    internal const float TankbusterSpikeFraction = 0.45f;
    internal const float SoftTankbusterSpikeFraction = 0.30f;
    internal const float RaidwideSpikeFraction = 0.20f;
    internal const float DefaultHorizonSeconds = 4f;
    internal const float TrashHorizonSeconds = 6f;
    internal const float DefaultSafetyHpPercent = 0.30f;
    internal const float TrashSafetyHpPercent = 0.40f;
    internal const float BossTankbusterSafetyHpPercent = 0.50f;
    internal const float InvulnRequiredReduction = 0.90f;

    internal static MitigationCoverageResult? SelectMinimumMitigation(
        MitigationCoverageRequest request,
        IReadOnlyList<MitigationOption> available,
        ActiveMitigationState active)
    {
        if (request.MaxHp == 0 || available.Count == 0)
            return null;

        if (active.InvulnActive)
            return null;

        var effectiveMaxHp = request.MaxHp * (1f + active.ActiveMaxHpBonusFraction);
        var effectiveCurrentHp = Math.Min(request.CurrentHp, effectiveMaxHp);
        var safetyPercent = request.ConfirmedTankbuster
            ? Math.Max(request.SafetyHpPercent, BossTankbusterSafetyHpPercent)
            : request.SafetyHpPercent;
        var hpTarget = effectiveMaxHp * safetyPercent;

        var netDps = Math.Max(0f, request.IncomingDps - request.IncomingHps) * Math.Max(1f, request.SustainMultiplier);
        var sustainDamage = netDps * request.HorizonSeconds;
        var spikeDamage = request.MaxHp * request.MechanicSpikeFraction;
        var incomingBudget = Math.Max(sustainDamage, spikeDamage);
        incomingBudget = Math.Max(0f, incomingBudget - active.ActiveShield);

        if (incomingBudget <= 0f)
            return null;

        var hpAfterHit = effectiveCurrentHp - incomingBudget * (1f - active.CombinedDamageReduction);
        if (hpAfterHit >= hpTarget && !request.ConfirmedTankbuster)
            return null;

        var requiredReduction = 1f - (effectiveCurrentHp - hpTarget) / incomingBudget;
        requiredReduction = Math.Clamp(requiredReduction, 0f, 0.99f);

        var reductionGap = Math.Max(0f, requiredReduction - active.CombinedDamageReduction);
        if (reductionGap <= 0.01f && !request.ConfirmedTankbuster)
            return null;

        // Manual sort: iterate available list, track best candidate by (tier, efficiency)
        MitigationOption bestOption = default;
        float bestScore = float.MaxValue;

        for (int i = 0; i < available.Count; i++)
        {
            var option = available[i];

            if (TankMitigationSelection.ShouldExcludeActiveCategory(option, active))
                continue;

            if (option.Tier == MitigationTier.Invuln)
            {
                if (reductionGap >= InvulnRequiredReduction || hpAfterHit <= 0f)
                {
                    return new MitigationCoverageResult(
                        option.ActionId,
                        requiredReduction,
                        active.CombinedDamageReduction,
                        incomingBudget,
                        "invuln_required");
                }

                continue;
            }

            var projectedReduction = CombineReduction(active.CombinedDamageReduction, option.DamageReduction);
            var projectedHp = effectiveCurrentHp - incomingBudget * (1f - projectedReduction) + request.MaxHp * option.MaxHpBonusFraction;

            if (projectedReduction + 0.01f >= requiredReduction || projectedHp >= hpTarget)
            {
                // Default: minimum tier. PreferHeavyMitigation: prefer Large (Damnation) when caller gated TB / Emergency+low HP.
                var tierRank = request.PreferHeavyMitigation
                    ? (int)MitigationTier.Large - (int)option.Tier
                    : (int)option.Tier;
                var score = tierRank * 100f +
                    (option.CooldownSeconds > 0 ? option.CooldownSeconds / Math.Max(option.DamageReduction, 0.01f) : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestOption = option;
                }
            }
        }

        if (bestOption.ActionId != 0)
        {
            return new MitigationCoverageResult(
                bestOption.ActionId,
                requiredReduction,
                active.CombinedDamageReduction,
                incomingBudget,
                $"tier={(int)bestOption.Tier}");
        }

        if (request.ConfirmedTankbuster)
        {
            // TB fallback: find lowest-tier Small/Medium option
            MitigationOption tbFallback = default;
            int lowestTier = int.MaxValue;

            for (int i = 0; i < available.Count; i++)
            {
                var option = available[i];
                if (TankMitigationSelection.ShouldExcludeActiveCategory(option, active))
                    continue;

                if ((option.Tier == MitigationTier.Small || option.Tier == MitigationTier.Medium)
                    && (int)option.Tier < lowestTier)
                {
                    lowestTier = (int)option.Tier;
                    tbFallback = option;
                }
            }

            if (tbFallback.ActionId != 0)
            {
                return new MitigationCoverageResult(
                    tbFallback.ActionId,
                    requiredReduction,
                    active.CombinedDamageReduction,
                    incomingBudget,
                    "tb_guarantee");
            }
        }

        // Invuln fallback
        for (int i = 0; i < available.Count; i++)
        {
            var option = available[i];
            if (option.Tier == MitigationTier.Invuln
                && (reductionGap >= InvulnRequiredReduction || hpAfterHit <= effectiveMaxHp * 0.15f))
            {
                return new MitigationCoverageResult(
                    option.ActionId,
                    requiredReduction,
                    active.CombinedDamageReduction,
                    incomingBudget,
                    "invuln_fallback");
            }
        }

        return null;
    }

    internal static float CombineReduction(float existing, float additional) =>
        1f - (1f - existing) * (1f - additional);
}
