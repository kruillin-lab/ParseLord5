using System.Collections.Generic;

namespace WrathCombo.Services.SmartMitigation;

internal static class TankMitigationSelection
{
    internal static bool IsLongMitigation(MitigationOption option) =>
        TrashMitigationOrdering.IsLongMitigationRecast(option.CooldownSeconds);

    internal static bool ShouldExcludeActiveCategory(
        MitigationOption option,
        ActiveMitigationState active) =>
        IsLongMitigation(option)
            ? active.LongMitigationActive
            : active.ShortMitigationActive;

    internal static bool TryPickLowestTier(
        List<MitigationOption> options,
        bool longMitigationBuffActive,
        System.Func<uint, bool> isLongMitigationAction,
        System.Func<uint, bool> passesGuards,
        ref uint actionId) =>
        TryPickInTierRange(
            options,
            MitigationTier.Small,
            MitigationTier.Large,
            preferHighestTier: false,
            longMitigationBuffActive,
            isLongMitigationAction,
            passesGuards,
            ref actionId);

    internal static bool TryPickInTierRange(
        List<MitigationOption> options,
        MitigationTier minTier,
        MitigationTier maxTier,
        bool preferHighestTier,
        bool longMitigationBuffActive,
        System.Func<uint, bool> isLongMitigationAction,
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

            if (TrashMitigationOrdering.ShouldExcludeLongMitigationOption(
                    longMitigationBuffActive,
                    IsLongMitigation(option) || isLongMitigationAction(option.ActionId)))
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
