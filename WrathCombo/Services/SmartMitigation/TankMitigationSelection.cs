using System.Collections.Generic;

namespace WrathCombo.Services.SmartMitigation;

internal static class TankMitigationSelection
{
    internal static bool TryPickLowestTier(
        List<MitigationOption> options,
        ActiveMitigationState activeState,
        bool isBoss,
        System.Func<uint, bool> passesGuards,
        ref uint actionId) =>
        TryPickInTierRange(
            options,
            MitigationTier.Small,
            MitigationTier.Large,
            preferHighestTier: false,
            activeState,
            isBoss,
            passesGuards,
            ref actionId);

    internal static bool TryPickTankMitigationFallback(
        List<MitigationOption> options,
        ActiveMitigationState activeState,
        bool isBoss,
        bool strictTankbuster,
        bool softTankbuster,
        bool sustainedPressure,
        bool fromTankCooldownHelper,
        System.Func<uint, bool> passesGuards,
        ref uint actionId)
    {
        if (strictTankbuster)
        {
            return TryPickLowestTier(
                options,
                activeState,
                isBoss,
                passesGuards,
                ref actionId);
        }

        return TryPickSustainedSoftTankbusterFallback(
            options,
            activeState,
            isBoss,
            softTankbuster,
            sustainedPressure,
            fromTankCooldownHelper,
            passesGuards,
            ref actionId);
    }

    internal static bool TryPickSustainedSoftTankbusterFallback(
        List<MitigationOption> options,
        ActiveMitigationState activeState,
        bool isBoss,
        bool softTankbuster,
        bool sustainedPressure,
        bool fromTankCooldownHelper,
        System.Func<uint, bool> passesGuards,
        ref uint actionId)
    {
        if (!isBoss || !softTankbuster || !sustainedPressure || fromTankCooldownHelper ||
            activeState.InvulnActive || activeState.LongPoolActive || activeState.ShortPoolActive)
            return false;

        return TryPickInTierRange(
            options,
            MitigationTier.Small,
            MitigationTier.Medium,
            preferHighestTier: false,
            activeState,
            isBoss,
            passesGuards,
            ref actionId);
    }

    internal static bool TryPickInTierRange(
        List<MitigationOption> options,
        MitigationTier minTier,
        MitigationTier maxTier,
        bool preferHighestTier,
        ActiveMitigationState activeState,
        bool isBoss,
        System.Func<uint, bool> passesGuards,
        ref uint actionId)
    {
        MitigationOption pick = default;
        var bestTier = preferHighestTier ? -1 : int.MaxValue;

        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var tier = (int)option.Tier;

            if (tier < (int)minTier || tier > (int)maxTier)
                continue;

            if (TrashMitigationOrdering.ShouldExcludeForStacking(option.Pool, activeState, isBoss))
                continue;

            if (preferHighestTier)
            {
                if (tier > bestTier)
                {
                    bestTier = tier;
                    pick = option;
                }
            }
            else if (tier < bestTier)
            {
                bestTier = tier;
                pick = option;
            }
        }

        if (pick.ActionId == 0 || !passesGuards(pick.ActionId))
            return false;

        actionId = pick.ActionId;
        return true;
    }
}
