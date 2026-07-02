using System.Collections.Generic;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Services.SmartMitigation;
using static WrathCombo.Combos.PvE.PLD.Config;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class PLD
{
    private static bool IsPldLongMitigationActive() =>
        HasStatusEffect(Buffs.HallowedGround) ||
        HasStatusEffect(Buffs.Sentinel) ||
        HasStatusEffect(Buffs.Guardian);

    private static bool IsPldLongMitigationAction(uint actionId) =>
        actionId == HallowedGround ||
        actionId == DivineVeil ||
        actionId == OriginalHook(Sentinel) ||
        actionId == Sentinel;

    private static bool IsPldHeavyMitigationAction(uint actionId) =>
        actionId == OriginalHook(Sentinel) || actionId == Sentinel;

    private static bool IsPldTrashMitigationThresholdBailout(bool isSimple) =>
        GetAvgEnemyHPPercentInRange(5f) <= (isSimple ? 10f : PLD_Mitigation_NonBoss_MitigationThreshold);

    private static bool ShouldSkipPldTrashMitigationStacking(TankThreatState threat, int enemyCount) =>
        IsPldLongMitigationActive() &&
        enemyCount <= 2 &&
        !threat.ConfirmedTankbuster &&
        threat.MechanicSpikeFraction < MitigationCoverageCalculator.SoftTankbusterSpikeFraction;

    private static List<MitigationOption> FilterPldMitigationOptions(
        List<MitigationOption> options,
        ActiveMitigationState active,
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

            if (option.ActionId == Bulwark && HasStatusEffect(Buffs.Bulwark))
                continue;

            if ((option.ActionId == OriginalHook(Sentinel) || option.ActionId == Sentinel) &&
                HasAnyStatusEffects([Buffs.Sentinel, Buffs.Guardian]))
                continue;

            if (option.ActionId == HallowedGround && active.InvulnActive)
                continue;

            if (option.Tier == MitigationTier.Invuln)
            {
                filtered.Add(option);
                continue;
            }

            if (active.InvulnActive)
                continue;

            if (TrashMitigationOrdering.ShouldExcludeLongMitigationOption(
                    IsPldLongMitigationActive(),
                    IsPldLongMitigationAction(option.ActionId)))
            {
                if (IsPldHeavyMitigationAction(option.ActionId) &&
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
