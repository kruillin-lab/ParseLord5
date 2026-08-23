using System.Collections.Generic;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Services.SmartMitigation;
using static WrathCombo.Combos.PvE.GNB.Config;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class GNB
{
    /// <summary>Long pool: heavy personal / party CDs that must not overwrite each other.
    /// Camouflage is GNB's designed stacking partner for Rampart, so it lives in the short pool.</summary>
    private static bool IsGnbLongMitigationActive() =>
        HasStatusEffect(Role.Buffs.Rampart) ||
        HasStatusEffect(Buffs.Nebula) ||
        HasStatusEffect(Buffs.GreatNebula) ||
        HasStatusEffect(Buffs.HeartOfLight);

    /// <summary>Short pool: may overlap the long pool. Heart of Stone/Corundum is Exempt, not short.</summary>
    private static bool IsGnbShortMitigationActive() =>
        HasStatusEffect(Buffs.Camouflage);

    /// <summary>Heart of Stone / Heart of Corundum mitigation buffs (Exempt pool — always stacks).</summary>
    private static bool IsGnbCorundumActive() =>
        HasAnyStatusEffects([Buffs.HeartOfStone, Buffs.HeartOfCorundum, Buffs.ClarityOfCorundum]);

    private static bool IsGnbLongMitigationAction(uint actionId) =>
        actionId == Superbolide ||
        actionId == HeartOfLight ||
        actionId == OriginalHook(Nebula) ||
        actionId == Nebula;

    private static bool IsGnbHeavyMitigationAction(uint actionId) =>
        actionId == OriginalHook(Nebula) || actionId == Nebula;

    private static bool IsGnbTrashMitigationThresholdBailout(bool isSimple) =>
        GetAvgEnemyHPPercentInRange(5f) <= (isSimple ? 10f : GNB_Mit_Advanced_NonBoss_MitigationThreshold);

    private static bool ShouldSkipGnbTrashMitigationStacking(TankThreatState threat, int enemyCount) =>
        IsGnbLongMitigationActive() &&
        enemyCount <= 2 &&
        !threat.ConfirmedTankbuster &&
        threat.MechanicSpikeFraction < MitigationCoverageCalculator.SoftTankbusterSpikeFraction;

    private static List<MitigationOption> FilterGnbMitigationOptions(
        List<MitigationOption> options,
        ActiveMitigationState active,
        bool isBoss,
        TankThreatState threat,
        uint currentHp,
        uint maxHp,
        PlayerPressureState pressure)
    {
        if (options.Count == 0)
            return options;

        var filtered = new List<MitigationOption>(options.Count);

        foreach (var option in options)
        {
            if (option.ActionId == Role.Rampart && HasStatusEffect(Role.Buffs.Rampart))
                continue;

            if (option.ActionId == Role.ArmsLength && HasStatusEffect(Role.Buffs.ArmsLength))
                continue;

            if (option.ActionId == Camouflage && HasStatusEffect(Buffs.Camouflage))
                continue;

            if (IsGnbHeavyMitigationAction(option.ActionId) &&
                HasAnyStatusEffects([Buffs.Nebula, Buffs.GreatNebula]))
                continue;

            if (option.ActionId == Superbolide && active.InvulnActive)
                continue;

            if (option.Tier == MitigationTier.Invuln)
            {
                filtered.Add(option);
                continue;
            }

            if (active.InvulnActive)
                continue;

            if (TrashMitigationOrdering.ShouldExcludeForStacking(option.Pool, active, isBoss))
            {
                if (IsGnbHeavyMitigationAction(option.ActionId) &&
                    TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp))
                {
                    filtered.Add(option);
                    continue;
                }

                continue;
            }

            filtered.Add(option);
        }

        return filtered;
    }
}
