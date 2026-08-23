using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Extensions;
using WrathCombo.Services;
using WrathCombo.Services.SmartMitigation;
using WrathCombo.Services.TankCooldownHelperIPC;
using static WrathCombo.Combos.PvE.WAR.Config;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class WAR
{
    private const long ParseLord5WarSmartMitTraceThrottleMs = 5_000;
    private static long _nextParseLord5WarSmartMitTraceAt;

    private static bool TrySmartBossMits(RotationMode rotationFlags, ref uint actionID) =>
        TrySmartMits(rotationFlags, isBoss: true, ref actionID);

    private static bool TrySmartNonBossMits(RotationMode rotationFlags, ref uint actionID) =>
        TrySmartMits(rotationFlags, isBoss: false, ref actionID);

    private static bool TrySmartMits(RotationMode rotationFlags, bool isBoss, ref uint actionID)
    {
        if (LocalPlayer is not { } player)
            return false;

        var pressure = TankSmartMitigationThreat.GetPlayerPressure((uint)player.GameObjectId);

        actionID = 0;

        if (IsWarBloodwhettingDefenseActive())
            return false;

        if (!isBoss && TrySmartNonBossEmergency(rotationFlags, pressure, ref actionID))
            return true;

        var threat = TankSmartMitigationThreat.Detect(isBoss, pressure);

        if (!TankSmartMitigationThreat.HasMitigationThreat(threat, isBoss, pressure))
        {
            var noThreatSource = TankCooldownHelperIpcClient.IsPluginLoaded &&
                                 !pressure.FromTankCooldownHelper
                ? TankCooldownHelperIpcClient.LastPressureSourceFailure ?? "tch_ipc_miss"
                : "no_threat";

            TraceSmartMitigation(0, null, pressure, threat, isBoss, noThreatSource);
            return false;
        }

        if (!isBoss &&
            TrySelectWarTrashReprisalFirst(rotationFlags, pressure, threat, ref actionID))
            return true;

        if (TrySelectSmartPersonalMitigation(
                rotationFlags,
                isBoss,
                player.CurrentHp,
                player.MaxHp,
                pressure,
                threat,
                ref actionID))
            return true;

        return TrySelectSmartPartyMitigation(rotationFlags, isBoss, threat, pressure, ref actionID);
    }

    private static bool TrySmartNonBossEmergency(
        RotationMode rotationFlags,
        PlayerPressureState pressure,
        ref uint actionID)
    {
        var holmgangThreshold = rotationFlags.HasFlag(RotationMode.simple)
            ? 10
            : WAR_Mitigation_NonBoss_Holmgang_Health;

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_Holmgang, rotationFlags) &&
            ActionReady(Holmgang) &&
            PlayerHealthPercentageHp() <= holmgangThreshold)
        {
            actionID = Holmgang;
            return true;
        }

        return false;
    }

    private static bool TrySelectSmartPersonalMitigation(
        RotationMode rotationFlags,
        bool isBoss,
        uint currentHp,
        uint maxHp,
        PlayerPressureState pressure,
        TankThreatState threat,
        ref uint actionID)
    {
        if (!InWarOgcdWindow || IsWarBloodwhettingDefenseActive() || UsedWarMitigationThisGcd)
            return false;

        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);

        if (!isBoss && IsWarTrashMitigationThresholdBailout(rotationFlags) && !threat.ConfirmedTankbuster)
            return false;

        if (!isBoss && !pressure.TankCooldownEmergency &&
            ShouldSkipWarTrashMitigationStacking(threat, enemyCount))
            return false;

        if (isBoss &&
            !ShouldOfferDamnation(threat, pressure, currentHp, maxHp) &&
            IsWarLongMitigationActive() &&
            !threat.ConfirmedTankbuster &&
            !threat.SoftTankbuster)
            return false;

        if (JustUsed(OriginalHook(Vengeance)) ||
            JustUsed(OriginalHook(RawIntuition)) ||
            JustUsed(Role.Reprisal) ||
            JustUsed(Role.ArmsLength) ||
            JustUsed(Role.Rampart) ||
            JustUsed(Holmgang))
            return false;

        var active = GetWarActiveMitigationState();
        var options = isBoss
            ? BuildWarBossMitigationOptions(rotationFlags, threat, pressure, currentHp, maxHp)
            : BuildWarNonBossMitigationOptions(rotationFlags, threat, pressure, currentHp, maxHp);

        options = FilterWarMitigationOptions(options, active, threat, isBoss, pressure, currentHp, maxHp);

        if (options.Count == 0)
            return false;

        var sustainMultiplier = isBoss
            ? 1f
            : 1f + Math.Min(enemyCount, 5) * 0.06f;

        var spikeFraction = TankSmartMitigationThreat.ResolveMechanicSpikeFraction(threat, pressure);

        var request = new MitigationCoverageRequest(
            currentHp,
            maxHp,
            pressure.IncomingDps,
            pressure.IncomingHps,
            spikeFraction,
            isBoss ? MitigationCoverageCalculator.DefaultHorizonSeconds : MitigationCoverageCalculator.TrashHorizonSeconds,
            isBoss ? MitigationCoverageCalculator.DefaultSafetyHpPercent : MitigationCoverageCalculator.TrashSafetyHpPercent,
            ConfirmedTankbuster: threat.ConfirmedTankbuster,
            SustainMultiplier: sustainMultiplier,
            PreferHeavyMitigation: ShouldOfferDamnation(threat, pressure, currentHp, maxHp));

        var selected = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, active, isBoss);
        var fallbackAction = 0u;
        if (selected is null &&
            !TrySelectWarMitigationFallback(options, threat, isBoss, ref fallbackAction) &&
            !TrySelectWarTchDangerFallback(options, threat, pressure, currentHp, maxHp, isBoss, ref fallbackAction))
        {
            TraceSmartMitigation(0, null, pressure, threat, isBoss, "coverage_skip", currentHp, maxHp);
            return false;
        }

        if (selected is null)
        {
            if (fallbackAction == 0)
                return false;

            var fallbackSource = HasIncomingTankBusterEffect(out _)
                ? "tb_fallback"
                : "tch_fallback";

            if (!PassesWarSmartMitigationGuards(fallbackAction))
                return false;

            actionID = fallbackAction;
            TraceSmartMitigation(actionID, null, pressure, threat, isBoss, fallbackSource, currentHp, maxHp);
            return true;
        }

        if (!PassesWarSmartMitigationGuards(selected!.Value.ActionId))
            return false;

        actionID = selected.Value.ActionId;
        TraceSmartMitigation(actionID, selected.Value, pressure, threat, isBoss, "personal", currentHp, maxHp);
        return true;
    }

    /// <summary>Trash: party Reprisal before personal long CDs; may overlap active long buffs once Reprisal is up.</summary>
    private static bool TrySelectWarTrashReprisalFirst(
        RotationMode rotationFlags,
        PlayerPressureState pressure,
        TankThreatState threat,
        ref uint actionID)
    {
        if (!InWarOgcdWindow)
            return false;

        if (IsWarBloodwhettingDefenseActive() || UsedWarMitigationThisGcd)
            return false;

        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);
        var reprisalReady = ActionReady(Role.Reprisal) && Role.CanReprisal(checkTargetForDebuff: false);

        if (!TrashMitigationOrdering.ShouldPrioritizeTrashReprisalFirst(
                isTrash: true,
                reprisalEnabled: IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_Reprisal, rotationFlags),
                reprisalReady: reprisalReady,
                reprisalRecentlyUsed: JustUsed(Role.Reprisal, 10f),
                enemyCountInRange: enemyCount))
            return false;

        actionID = Role.Reprisal;
        TraceSmartMitigation(actionID, null, pressure, threat, isBoss: false, "trash_reprisal_first");
        return true;
    }

    private static bool TrySelectSmartPartyMitigation(
        RotationMode rotationFlags,
        bool isBoss,
        TankThreatState threat,
        PlayerPressureState pressure,
        ref uint actionID)
    {
        // Party mits are oGCDs too: without this the AoE path fires outside the oGCD
        // window and clips the GCD, while the personal path (window-gated) is skipped.
        if (!InWarOgcdWindow)
            return false;

        if (IsWarBloodwhettingDefenseActive() || UsedWarMitigationThisGcd)
            return false;

        if (!threat.Raidwide && (!isBoss || pressure.DangerRatio < 1.0f))
            return false;

        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);
        if (!isBoss && enemyCount < 3)
            return false;

        if (isBoss)
        {
            var reprisalInMitigationContent = rotationFlags.HasFlag(RotationMode.simple) ||
                                              ContentCheck.IsInConfiguredContent(WAR_Mitigation_Boss_Reprisal_Difficulty, WAR_Boss_Mit_DifficultyListSet);

            if (IsSmartMitEnabled(Preset.WAR_Mitigation_Boss_Reprisal, rotationFlags) &&
                Role.CanReprisal(enemyCount: 1) &&
                reprisalInMitigationContent &&
                !JustUsed(ShakeItOff, 10f) &&
                threat.Raidwide &&
                pressure.DangerRatio >= 1.0f)
            {
                actionID = Role.Reprisal;
                TraceSmartMitigation(actionID, null, pressure, threat, isBoss, "party_reprisal");
                return true;
            }

            var shakeItOffInMitigationContent = rotationFlags.HasFlag(RotationMode.simple) ||
                                                ContentCheck.IsInConfiguredContent(WAR_Mitigation_Boss_ShakeItOff_Difficulty, WAR_Boss_Mit_DifficultyListSet);

            if (IsSmartMitEnabled(Preset.WAR_Mitigation_Boss_ShakeItOff, rotationFlags) &&
                !JustUsed(Role.Reprisal, 10f) &&
                !IsWarLongMitigationActive() &&
                shakeItOffInMitigationContent &&
                ActionReady(ShakeItOff) &&
                threat.Raidwide && // Shake It Off is a party shield — only auto-cast on detected AoE/raidwide
                pressure.DangerRatio >= 1.5f)
            {
                actionID = ShakeItOff;
                TraceSmartMitigation(actionID, null, pressure, threat, isBoss, "party_shake");
                return true;
            }

            return false;
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_Reprisal, rotationFlags) &&
            ActionReady(Role.Reprisal) &&
            enemyCount >= 3 &&
            !JustUsed(Role.Reprisal, 10f) &&
            pressure.DangerRatio >= 1.2f)
        {
            actionID = Role.Reprisal;
            TraceSmartMitigation(actionID, null, pressure, threat, isBoss, "trash_reprisal");
            return true;
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_ShakeItOff, rotationFlags) &&
            ActionReady(ShakeItOff) &&
            !IsWarLongMitigationActive() &&
            !JustUsed(Role.Reprisal, 10f) &&
            threat.Raidwide && // Shake It Off is a party shield — only auto-cast on detected AoE/raidwide
            pressure.DangerRatio >= 1.5f)
        {
            actionID = ShakeItOff;
            TraceSmartMitigation(actionID, null, pressure, threat, isBoss, "trash_shake");
            return true;
        }

        return false;
    }


    /// <summary>
    /// TCH danger fallback when coverage returns null. Uses smallest CD unless Damnation gate passes.
    /// </summary>
    private static bool TrySelectWarTchDangerFallback(
        List<MitigationOption> options,
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp,
        bool isBoss,
        ref uint actionID)
    {
        if (!pressure.FromTankCooldownHelper || !pressure.TankCooldownInDanger)
            return false;

        if (options.Count == 0)
            return false;

        if (ShouldOfferDamnation(threat, pressure, currentHp, maxHp) &&
            TryPickDamnationFromOptions(options, ref actionID))
            return true;

        return TryPickLowestTierWarMitigation(options, isBoss, ref actionID);
    }

    private static bool TryPickDamnationFromOptions(List<MitigationOption> options, ref uint actionID)
    {
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            if (!IsWarVengeanceOrDamnationAction(option.ActionId))
                continue;

            if (!PassesWarSmartMitigationGuards(option.ActionId))
                continue;

            actionID = option.ActionId;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Damnation/Vengeance: strict tankbuster telegraph only, or TCH Emergency in combat while below 50% HP.
    /// Soft tankbuster (targeted) and sustained pressure alone never qualify.
    /// </summary>
    private static bool ShouldOfferDamnation(
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        _ = threat;
        if (HasIncomingTankBusterEffect(out _))
            return true;

        return ShouldOfferDamnationForTchEmergency(pressure, currentHp, maxHp);
    }

    private static bool ShouldOfferDamnationForTchEmergency(
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        if (!InCombat() || maxHp == 0)
            return false;

        if (!pressure.FromTankCooldownHelper || !pressure.TankCooldownEmergency)
            return false;

        return currentHp * 100u < maxHp * 50u;
    }

    private static string? GetDamnationGateTraceReason(
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        if (HasIncomingTankBusterEffect(out _))
            return "tb_confirmed";

        if (ShouldOfferDamnationForTchEmergency(pressure, currentHp, maxHp))
            return "tch_emergency_lowhp";

        return null;
    }

    /// <summary>TB telegraph safety net — smallest CD only (Damnation not in option list without gate).</summary>
    private static bool TrySelectWarMitigationFallback(
        List<MitigationOption> options,
        TankThreatState threat,
        bool isBoss,
        ref uint actionID)
    {
        _ = threat;
        _ = isBoss;
        if (options.Count == 0)
            return false;

        if (!HasIncomingTankBusterEffect(out _))
            return false;

        return TryPickLowestTierWarMitigation(options, isBoss, ref actionID);
    }

    private static bool TryPickLowestTierWarMitigation(List<MitigationOption> options, bool isBoss, ref uint actionID) =>
        TryPickWarMitigationInTierRange(options, MitigationTier.Small, MitigationTier.Large, preferHighestTier: false, isBoss, ref actionID);

    private static bool TryPickWarMitigationInTierRange(
        List<MitigationOption> options,
        MitigationTier minTier,
        MitigationTier maxTier,
        bool preferHighestTier,
        bool isBoss,
        ref uint actionID)
    {
        MitigationOption pick = default;
        var bestTier = preferHighestTier ? -1 : int.MaxValue;
        var active = GetWarActiveMitigationState();

        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var tier = (int)option.Tier;

            if (tier < (int)minTier || tier > (int)maxTier)
                continue;

            if (TrashMitigationOrdering.ShouldExcludeForStacking(option.Pool, active, isBoss))
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

        if (pick.ActionId == 0 || !PassesWarSmartMitigationGuards(pick.ActionId))
            return false;

        actionID = pick.ActionId;
        return true;
    }

    private static List<MitigationOption> BuildWarBossMitigationOptions(
        RotationMode rotationFlags,
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        var options = new List<MitigationOption>();

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_Boss_RawIntuition_TankBuster, rotationFlags) &&
            ActionReady(OriginalHook(RawIntuition)) &&
            (rotationFlags.HasFlag(RotationMode.simple) ||
             ContentCheck.IsInConfiguredContent(WAR_Mitigation_Boss_RawIntuition_TankBuster_Difficulty, WAR_Boss_Mit_DifficultyListSet)) &&
            (threat.ConfirmedTankbuster || threat.SoftTankbuster))
        {
            options.Add(new MitigationOption(
                OriginalHook(RawIntuition),
                0.10f,
                0.10f,
                0f,
                25f,
                MitigationTier.Small,
                MitigationPool.Short));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_Boss_RawIntuition_OnCD, rotationFlags) &&
            ActionReady(OriginalHook(RawIntuition)) &&
            (rotationFlags.HasFlag(RotationMode.simple) ||
             ContentCheck.IsInConfiguredContent(WAR_Mitigation_Boss_RawIntuition_OnCD_Difficulty, WAR_Boss_Mit_DifficultyListSet)) &&
            IsPlayerTargeted())
        {
            options.Add(new MitigationOption(
                OriginalHook(RawIntuition),
                0.10f,
                0.10f,
                0f,
                25f,
                MitigationTier.Small,
                MitigationPool.Short));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_Boss_Rampart, rotationFlags) &&
            ActionReady(Role.Rampart) &&
            (rotationFlags.HasFlag(RotationMode.simple) ||
             ContentCheck.IsInConfiguredContent(WAR_Mitigation_Boss_Rampart_Difficulty, WAR_Boss_Mit_DifficultyListSet)) &&
            (threat.ConfirmedTankbuster || threat.SoftTankbuster || threat.SustainedPressure))
        {
            options.Add(new MitigationOption(Role.Rampart, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Long));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_Boss_Vengeance, rotationFlags) &&
            ActionReady(OriginalHook(Vengeance)) &&
            !HasAnyStatusEffects([Buffs.Vengeance, Buffs.Damnation]) &&
            (rotationFlags.HasFlag(RotationMode.simple) ||
             ContentCheck.IsInConfiguredContent(WAR_Mitigation_Boss_Vengeance_Difficulty, WAR_Boss_Mit_DifficultyListSet)) &&
            ShouldOfferDamnation(threat, pressure, currentHp, maxHp))
        {
            var vengeanceAction = OriginalHook(Vengeance);
            var isDamnation = vengeanceAction != Vengeance;
            options.Add(new MitigationOption(
                vengeanceAction,
                isDamnation ? 0.40f : 0.30f,
                0f,
                0f,
                120f,
                MitigationTier.Large,
                MitigationPool.Long));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mit_Holmgang_Max, rotationFlags) &&
            ActionReady(Holmgang) &&
            threat.ConfirmedTankbuster &&
            (rotationFlags.HasFlag(RotationMode.simple) ||
             ContentCheck.IsInConfiguredContent(WAR_Mit_Holmgang_Max_Difficulty, WAR_Mit_Holmgang_Max_DifficultyListSet)))
        {
            options.Add(new MitigationOption(Holmgang, 1f, 0f, 0f, 240f, MitigationTier.Invuln, MitigationPool.Exempt));
        }

        return options;
    }

    private static List<MitigationOption> BuildWarNonBossMitigationOptions(
        RotationMode rotationFlags,
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        var options = new List<MitigationOption>();
        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_RawIntuition, rotationFlags) &&
            ActionReady(OriginalHook(RawIntuition)) &&
            (threat.SustainedPressure || threat.MechanicSpikeFraction >= 0.20f || enemyCount >= 5))
        {
            options.Add(new MitigationOption(
                OriginalHook(RawIntuition),
                0.10f,
                0.10f,
                0f,
                25f,
                MitigationTier.Small,
                MitigationPool.Short));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_Rampart, rotationFlags) &&
            Role.CanRampart() &&
            (enemyCount >= 3 || threat.ConfirmedTankbuster || threat.SustainedPressure))
        {
            options.Add(new MitigationOption(Role.Rampart, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Long));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_ArmsLength, rotationFlags) &&
            ActionReady(Role.ArmsLength) &&
            enemyCount >= 3)
        {
            options.Add(new MitigationOption(Role.ArmsLength, 0.20f, 0f, 0f, 120f, MitigationTier.Medium, MitigationPool.TrashOnly));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_Vengeance, rotationFlags) &&
            ActionReady(OriginalHook(Vengeance)) &&
            !HasAnyStatusEffects([Buffs.Vengeance, Buffs.Damnation]) &&
            ShouldOfferDamnation(threat, pressure, currentHp, maxHp))
        {
            var vengeanceAction = OriginalHook(Vengeance);
            var isDamnation = vengeanceAction != Vengeance;
            options.Add(new MitigationOption(
                vengeanceAction,
                isDamnation ? 0.40f : 0.30f,
                0f,
                0f,
                120f,
                MitigationTier.Large,
                MitigationPool.Long));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_Holmgang, rotationFlags) &&
            ActionReady(Holmgang) &&
            threat.ConfirmedTankbuster)
        {
            options.Add(new MitigationOption(Holmgang, 1f, 0f, 0f, 240f, MitigationTier.Invuln, MitigationPool.Exempt));
        }

        return options;
    }

    private static ActiveMitigationState GetWarActiveMitigationState()
    {
        var reduction = 0f;
        var shield = 0f;
        var maxHpBonus = 0f;
        var invuln = false;
        var longPoolActive = IsWarLongMitigationActive();
        var shortPoolActive = IsWarShortMitigationActive();

        if (HasStatusEffect(Buffs.Holmgang))
            invuln = true;

        if (HasStatusEffect(Role.Buffs.Rampart))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.20f);

        if (HasStatusEffect(Role.Buffs.ArmsLength))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.20f);

        if (HasStatusEffect(Buffs.Vengeance))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.30f);

        if (HasStatusEffect(Buffs.Damnation))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.40f);

        if (HasStatusEffect(Buffs.ShakeItOff) && LocalPlayer is { } shakePlayer && shakePlayer.MaxHp > 0)
            shield += shakePlayer.MaxHp * 0.15f;

        if (HasAnyStatusEffects([Buffs.BloodwhettingDefenseLong, Buffs.BloodwhettingDefenseShort]))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.10f);

        if (HasStatusEffect(Buffs.BloodwhettingShield) && LocalPlayer is { } player && player.MaxHp > 0)
            shield += player.MaxHp * 0.10f;

        return new ActiveMitigationState(reduction, shield, maxHpBonus, invuln, longPoolActive, shortPoolActive);
    }

    private static bool PassesWarSmartMitigationGuards(uint selectedActionId)
    {
        if (PerGcdActionCaps.ShouldHold(selectedActionId, IsWarMitigationAction))
            return false;

        if (selectedActionId == OriginalHook(Vengeance))
            return !JustUsed(Role.Rampart, 20f);

        if (selectedActionId == Role.Rampart)
            return !JustUsed(OriginalHook(Vengeance), 15f);

        return true;
    }

    private static bool UsedWarMitigationThisGcd =>
        PerGcdActionCaps.AnyUsed(IsWarMitigationAction);

    private static bool IsWarMitigationAction(uint action) =>
        action is ThrillOfBattle or Vengeance or Holmgang or RawIntuition or Equilibrium or ShakeItOff or
            NascentFlash or Bloodwhetting or Damnation ||
        action == OriginalHook(Vengeance) ||
        action == OriginalHook(RawIntuition) ||
        action == Role.Rampart ||
        action == Role.Reprisal ||
        action == Role.ArmsLength;

    private static bool IsSmartMitEnabled(Preset preset, RotationMode rotationFlags) =>
        rotationFlags.HasFlag(RotationMode.simple) || CustomComboFunctions.IsEnabled(preset);

    private static void TraceSmartMitigation(
        uint selectedActionId,
        MitigationCoverageResult? coverage,
        PlayerPressureState pressure,
        TankThreatState threat,
        bool isBoss,
        string source,
        uint currentHp = 0,
        uint maxHp = 0)
    {
        var now = Environment.TickCount64;
        if (now < _nextParseLord5WarSmartMitTraceAt)
            return;

        _nextParseLord5WarSmartMitTraceAt = now + ParseLord5WarSmartMitTraceThrottleMs;

        var damnationGate = selectedActionId != 0 &&
            IsWarVengeanceOrDamnationAction(selectedActionId) &&
            maxHp > 0
            ? GetDamnationGateTraceReason(pressure, currentHp, maxHp) ?? "ungated"
            : null;

        var gateSuffix = damnationGate is null ? string.Empty : $" damnation_gate={damnationGate}";
        var context = isBoss ? "boss" : "trash";

        if (coverage is { } result)
        {
            Svc.Log.Debug(
                "[ParseLord5][WAR_SmartMit] " +
                $"ctx={context} source={source}{gateSuffix} action={selectedActionId.ActionName()}({selectedActionId}) " +
                $"reason={result.Reason} budget={result.IncomingDamageBudget:F0} " +
                $"requiredR={result.RequiredReduction:F2} tb={threat.ConfirmedTankbuster} softTb={threat.SoftTankbuster} " +
                $"spike={threat.MechanicSpikeFraction:F2} netDps={pressure.NetDps:F0} ratio={pressure.DangerRatio:F2}");
            return;
        }

        var tchLevel = pressure.TankCooldownDangerLevel?.ToString() ?? "-";
        var pressureSource = pressure.FromTankCooldownHelper
            ? "tch"
            : TankCooldownHelperIpcClient.IsPluginLoaded
                ? TankCooldownHelperIpcClient.LastPressureSourceFailure ?? "telemetry"
                : "telemetry";

        var actionLabel = selectedActionId == 0
            ? "none"
            : $"{selectedActionId.ActionName()}({selectedActionId})";

        Svc.Log.Debug(
            "[ParseLord5][WAR_SmartMit] " +
            $"ctx={context} source={source}{gateSuffix} action={actionLabel} tb={threat.ConfirmedTankbuster} softTb={threat.SoftTankbuster} " +
            $"spike={threat.MechanicSpikeFraction:F2} netDps={pressure.NetDps:F0} ratio={pressure.DangerRatio:F2} " +
            $"tchLvl={tchLevel} pressureSrc={pressureSource} hp={currentHp}/{maxHp}");
    }
}
