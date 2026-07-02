using System.Collections.Generic;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Services.SmartMitigation;
using static WrathCombo.Combos.PvE.DRK.Config;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class DRK
{
    private static bool IsDrkLongMitigationActive() =>
        HasAnyStatusEffects([Buffs.LivingDead, Buffs.WalkingDead, Buffs.UndeadRebirth]) ||
        HasStatusEffect(Buffs.ShadowWall) ||
        HasStatusEffect(Buffs.ShadowedVigil) ||
        HasStatusEffect(Buffs.DarkMissionary);

    private static bool IsDrkLongMitigationAction(uint actionId) =>
        actionId == LivingDead ||
        actionId == DarkMissionary ||
        actionId == OriginalHook(ShadowWall) ||
        actionId == ShadowWall;

    private static bool IsDrkHeavyMitigationAction(uint actionId) =>
        actionId == OriginalHook(ShadowWall) || actionId == ShadowWall;

    private static bool IsDrkTrashMitigationThresholdBailout(bool isSimple) =>
        GetAvgEnemyHPPercentInRange(10f) <= (isSimple ? 10f : DRK_Mit_NonBoss_Threshold);

    private static bool ShouldSkipDrkTrashMitigationStacking(TankThreatState threat, int enemyCount) =>
        IsDrkLongMitigationActive() &&
        enemyCount <= 2 &&
        !threat.ConfirmedTankbuster &&
        threat.MechanicSpikeFraction < MitigationCoverageCalculator.SoftTankbusterSpikeFraction;

    private static List<MitigationOption> FilterDrkMitigationOptions(
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

            if (option.ActionId == DarkMind && HasStatusEffect(Buffs.DarkMind))
                continue;

            if (IsDrkHeavyMitigationAction(option.ActionId) &&
                HasAnyStatusEffects([Buffs.ShadowWall, Buffs.ShadowedVigil]))
                continue;

            if (option.ActionId == LivingDead && active.InvulnActive)
                continue;

            if (option.Tier == MitigationTier.Invuln)
            {
                filtered.Add(option);
                continue;
            }

            if (active.InvulnActive)
                continue;

            if (TrashMitigationOrdering.ShouldExcludeLongMitigationOption(
                    IsDrkLongMitigationActive(),
                    IsDrkLongMitigationAction(option.ActionId)))
            {
                if (IsDrkHeavyMitigationAction(option.ActionId) &&
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
