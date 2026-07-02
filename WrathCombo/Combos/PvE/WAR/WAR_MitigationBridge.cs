using System.Collections.Generic;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Services.SmartMitigation;
using static WrathCombo.Combos.PvE.WAR.Config;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

/// <summary>
/// Aligns smart mitigation with the Auto Mitigation System in <see cref="WAR_Helper"/>.
/// </summary>
internal partial class WAR
{
    /// <summary>Recast at or above this value is a "long" CD — longs do not overlap longs; shorts may overlap.</summary>
    private const float WarLongMitigationRecastSeconds = TrashMitigationOrdering.LongMitigationRecastSeconds;

    /// <summary>Matches helper trash bail — includes Reprisal on target.</summary>
    private static bool IsWarMitigationRunning() =>
        IsWarPersonalMitigationActive() ||
        HasStatusEffect(Role.Debuffs.Reprisal, CurrentTarget);

    /// <summary>Any personal mitigation buff active (short or long).</summary>
    private static bool IsWarPersonalMitigationActive() =>
        IsWarShortMitigationActive() || IsWarLongMitigationActive();

    /// <summary>Short CDs that may freely overlap each other and the long mits. Only Arm's Length now —
    /// Rampart (90s recast) is treated as long, and only stacks with Arm's Length.</summary>
    private static bool IsWarShortMitigationActive() =>
        HasStatusEffect(Role.Buffs.ArmsLength);

    /// <summary>Long CDs (&gt;60s recast: Rampart, Holmgang, Shake, Vengeance-Damnation): only one long at a
    /// time, so no long mit overwrites another. Arm's Length (short) and Reprisal are not included.</summary>
    private static bool IsWarLongMitigationActive() =>
        HasStatusEffect(Role.Buffs.Rampart) ||
        HasStatusEffect(Buffs.Holmgang) ||
        HasStatusEffect(Buffs.Vengeance) ||
        HasStatusEffect(Buffs.Damnation) ||
        HasStatusEffect(Buffs.ShakeItOff);

    /// <summary>Bloodwhetting defensive buff — smart mit holds all tank CDs until it expires.</summary>
    private static bool IsWarBloodwhettingDefenseActive() =>
        HasAnyStatusEffects([Buffs.BloodwhettingDefenseLong, Buffs.BloodwhettingDefenseShort]);

    /// <summary>Long mit actions for overlap (Reprisal/Rampart/Arms/RawIntuition are short — may overlap long buffs).</summary>
    private static bool IsWarLongMitigationAction(uint actionId) =>
        actionId == Holmgang ||
        actionId == ShakeItOff ||
        IsWarVengeanceOrDamnationAction(actionId);

    private static bool IsWarLongMitigationRecast(float cooldownSeconds) =>
        TrashMitigationOrdering.IsLongMitigationRecast(cooldownSeconds);

    private static bool IsWarTrashMitigationThresholdBailout(RotationMode rotationFlags)
    {
        var threshold = rotationFlags.HasFlag(RotationMode.simple)
            ? 10f
            : WAR_Mitigation_NonBoss_MitigationThreshold;

        return GetAvgEnemyHPPercentInRange(10f) <= threshold;
    }

    private static bool ShouldSkipWarTrashMitigationStacking(
        TankThreatState threat,
        int enemyCount) =>
        IsWarLongMitigationActive() &&
        enemyCount <= 2 &&
        !threat.ConfirmedTankbuster &&
        threat.MechanicSpikeFraction < MitigationCoverageCalculator.SoftTankbusterSpikeFraction;

    private static List<MitigationOption> FilterWarMitigationOptions(
        List<MitigationOption> options,
        ActiveMitigationState active,
        TankThreatState threat,
        bool isBoss,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        if (options.Count == 0)
            return options;

        var filtered = new List<MitigationOption>(options.Count);

        foreach (var option in options)
        {
            if (ShouldIncludeWarMitigationOption(option, active, threat, isBoss, pressure, currentHp, maxHp))
                filtered.Add(option);
        }

        return filtered;
    }

    private static bool ShouldIncludeWarMitigationOption(
        MitigationOption option,
        ActiveMitigationState active,
        TankThreatState threat,
        bool isBoss,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        if (option.ActionId == Role.Rampart && HasStatusEffect(Role.Buffs.Rampart))
            return false;

        if (option.ActionId == Role.ArmsLength && HasStatusEffect(Role.Buffs.ArmsLength))
            return false;

        if ((option.ActionId == OriginalHook(Vengeance) || option.ActionId == Vengeance) &&
            HasAnyStatusEffects([Buffs.Vengeance, Buffs.Damnation]))
            return false;

        if (option.ActionId == Holmgang && active.InvulnActive)
            return false;

        if (option.Tier == MitigationTier.Invuln)
            return true;

        if (active.InvulnActive)
            return false;

        // Long mitigation: no second long while another long buff is active (shorts may still fire).
        // A "long" mit is any cooldown over 60s — driven by the option's recast, not a hardcoded list,
        // so e.g. Rampart (90s) will not overwrite an active Vengeance/Damnation/Holmgang/Shake (>60s).
        if (TrashMitigationOrdering.ShouldExcludeLongMitigationOption(
                IsWarLongMitigationActive(),
                IsWarLongMitigationAction(option.ActionId) || IsWarLongMitigationRecast(option.CooldownSeconds)))
        {
            if (IsWarVengeanceOrDamnationAction(option.ActionId) &&
                ShouldOfferDamnation(threat, pressure, currentHp, maxHp))
                return true;

            return false;
        }

        return true;
    }

    private static bool IsWarVengeanceOrDamnationAction(uint actionId) =>
        actionId == OriginalHook(Vengeance) || actionId == Vengeance;

    private static List<MitigationOption> BuildWarPartyMitigationOptions(
        RotationMode rotationFlags,
        bool isBoss,
        TankThreatState threat,
        int enemyCount)
    {
        var options = new List<MitigationOption>(2);

        if (isBoss)
        {
            var reprisalInContent = rotationFlags.HasFlag(RotationMode.simple) ||
                                    ContentCheck.IsInConfiguredContent(WAR_Mitigation_Boss_Reprisal_Difficulty, WAR_Boss_Mit_DifficultyListSet);

            if (IsSmartMitEnabled(Preset.WAR_Mitigation_Boss_Reprisal, rotationFlags) &&
                Role.CanReprisal(enemyCount: 1) &&
                reprisalInContent &&
                !JustUsed(ShakeItOff, 10f) &&
                (threat.Raidwide || threat.ConfirmedTankbuster))
            {
                options.Add(new MitigationOption(Role.Reprisal, 0.10f, 0f, 0f, 60f, MitigationTier.Small));
            }

            var shakeInContent = rotationFlags.HasFlag(RotationMode.simple) ||
                                 ContentCheck.IsInConfiguredContent(WAR_Mitigation_Boss_ShakeItOff_Difficulty, WAR_Boss_Mit_DifficultyListSet);

            if (IsSmartMitEnabled(Preset.WAR_Mitigation_Boss_ShakeItOff, rotationFlags) &&
                shakeInContent &&
                ActionReady(ShakeItOff) &&
                !JustUsed(Role.Reprisal, 10f) &&
                !IsWarLongMitigationActive() &&
                (threat.Raidwide || threat.ConfirmedTankbuster))
            {
                options.Add(new MitigationOption(ShakeItOff, 0f, 0f, 0f, 90f, MitigationTier.Medium));
            }
        }
        else
        {
            if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_Reprisal, rotationFlags) &&
                ActionReady(Role.Reprisal) &&
                enemyCount >= 3 &&
                !JustUsed(Role.Reprisal, 10f) &&
                (threat.Raidwide || threat.SustainedPressure))
            {
                options.Add(new MitigationOption(Role.Reprisal, 0.10f, 0f, 0f, 60f, MitigationTier.Small));
            }

            if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_ShakeItOff, rotationFlags) &&
                ActionReady(ShakeItOff) &&
                enemyCount >= 3 &&
                !JustUsed(Role.Reprisal, 10f) &&
                !IsWarLongMitigationActive() &&
                threat.Raidwide)
            {
                options.Add(new MitigationOption(ShakeItOff, 0f, 0f, 0f, 90f, MitigationTier.Medium));
            }
        }

        return options;
    }
}
