using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Extensions;
using WrathCombo.Services;
using WrathCombo.Services.SmartMitigation;
using static WrathCombo.Combos.PvE.WAR.Config;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class WAR
{
    private const long ParseLord5WarSmartMitTraceThrottleMs = 5_000;
    private static long _nextParseLord5WarSmartMitTraceAt;

    private readonly record struct WarThreatState(
        bool ConfirmedTankbuster,
        bool SoftTankbuster,
        bool Raidwide,
        float MechanicSpikeFraction,
        bool SustainedPressure);

    private static bool TrySmartBossMits(RotationMode rotationFlags, ref uint actionID) =>
        TrySmartMits(rotationFlags, isBoss: true, ref actionID);

    private static bool TrySmartNonBossMits(RotationMode rotationFlags, ref uint actionID) =>
        TrySmartMits(rotationFlags, isBoss: false, ref actionID);

    private static bool TrySmartMits(RotationMode rotationFlags, bool isBoss, ref uint actionID)
    {
        if (LocalPlayer is not { } player)
            return false;

        var pressure = CombatTelemetryService.GetPlayerPressure((uint)player.GameObjectId);

        if (!isBoss && TrySmartNonBossEmergency(rotationFlags, pressure, ref actionID))
            return true;

        var threat = DetectWarThreat(isBoss, pressure);

        if (!threat.SustainedPressure && threat.MechanicSpikeFraction <= 0f)
        {
            TraceSmartMitigation(0, null, pressure, threat, isBoss, "no_threat");
            return false;
        }

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

    private static WarThreatState DetectWarThreat(bool isBoss, PlayerPressureState pressure)
    {
        var confirmedTankbuster = HasIncomingTankBusterEffect(out _);

        if (!confirmedTankbuster &&
            TryGetTankBusterTarget(out IBattleChara tbTarget) &&
            LocalPlayer is { } player &&
            tbTarget.GameObjectId == player.GameObjectId)
        {
            confirmedTankbuster = true;
        }

        var softTankbuster = !confirmedTankbuster && isBoss && IsPlayerTargeted();
        var raidwide = GroupDamageIncoming();

        var mechanicSpikeFraction = 0f;
        if (confirmedTankbuster)
            mechanicSpikeFraction = Math.Max(mechanicSpikeFraction, MitigationCoverageCalculator.TankbusterSpikeFraction);
        else if (softTankbuster)
            mechanicSpikeFraction = Math.Max(mechanicSpikeFraction, MitigationCoverageCalculator.SoftTankbusterSpikeFraction);

        if (raidwide)
            mechanicSpikeFraction = Math.Max(mechanicSpikeFraction, MitigationCoverageCalculator.RaidwideSpikeFraction);

        if (LocalPlayer is { MaxHp: > 0 } hpPlayer && pressure.MaxSingleHit > 0f)
            mechanicSpikeFraction = Math.Max(mechanicSpikeFraction, pressure.MaxSingleHit / hpPlayer.MaxHp);

        var deathTimerThreshold = isBoss ? 10f : 12f;
        var sustainedPressure = pressure.NetDps > 0f &&
            (pressure.DangerRatio >= (isBoss ? 0.8f : 1.0f) ||
             (pressure.SecondsUntilDeath is { } ttd && ttd <= deathTimerThreshold) ||
             confirmedTankbuster);

        return new WarThreatState(confirmedTankbuster, softTankbuster, raidwide, mechanicSpikeFraction, sustainedPressure);
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

        var equilibriumThreshold = rotationFlags.HasFlag(RotationMode.simple)
            ? 65
            : WAR_Mitigation_NonBoss_Equilibrium_Health;

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_Equilibrium, rotationFlags) &&
            ActionReady(Equilibrium) &&
            PlayerHealthPercentageHp() <= equilibriumThreshold &&
            pressure.DangerRatio >= 1.0f)
        {
            actionID = Equilibrium;
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
        WarThreatState threat,
        ref uint actionID)
    {
        if (!InWarOgcdWindow)
            return false;

        if (JustUsed(OriginalHook(ThrillOfBattle)) ||
            JustUsed(OriginalHook(Vengeance)) ||
            JustUsed(OriginalHook(RawIntuition)) ||
            JustUsed(Role.Reprisal) ||
            JustUsed(Role.ArmsLength) ||
            JustUsed(Role.Rampart) ||
            JustUsed(Holmgang))
            return false;

        var active = GetWarActiveMitigationState();
        var options = isBoss
            ? BuildWarBossMitigationOptions(rotationFlags, threat)
            : BuildWarNonBossMitigationOptions(rotationFlags, threat);

        if (options.Count == 0)
            return false;

        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);
        var sustainMultiplier = isBoss ? 1f : 1f + Math.Min(enemyCount, 8) * 0.12f;

        var request = new MitigationCoverageRequest(
            currentHp,
            maxHp,
            pressure.IncomingDps,
            pressure.IncomingHps,
            threat.MechanicSpikeFraction,
            isBoss ? MitigationCoverageCalculator.DefaultHorizonSeconds : MitigationCoverageCalculator.TrashHorizonSeconds,
            isBoss ? MitigationCoverageCalculator.DefaultSafetyHpPercent : MitigationCoverageCalculator.TrashSafetyHpPercent,
            ConfirmedTankbuster: threat.ConfirmedTankbuster,
            SustainMultiplier: sustainMultiplier);

        var selected = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, active);
        if (selected is null)
        {
            TraceSmartMitigation(0, null, pressure, threat, isBoss, "coverage_skip");
            return false;
        }

        if (!PassesWarSmartMitigationGuards(selected.Value.ActionId))
            return false;

        actionID = selected.Value.ActionId;
        TraceSmartMitigation(actionID, selected.Value, pressure, threat, isBoss, "personal");
        return true;
    }

    private static bool TrySelectSmartPartyMitigation(
        RotationMode rotationFlags,
        bool isBoss,
        WarThreatState threat,
        PlayerPressureState pressure,
        ref uint actionID)
    {
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
                (threat.Raidwide || pressure.DangerRatio >= 1.0f))
            {
                actionID = Role.Reprisal;
                TraceSmartMitigation(actionID, null, pressure, threat, isBoss, "party_reprisal");
                return true;
            }

            var shakeItOffInMitigationContent = rotationFlags.HasFlag(RotationMode.simple) ||
                                                ContentCheck.IsInConfiguredContent(WAR_Mitigation_Boss_ShakeItOff_Difficulty, WAR_Boss_Mit_DifficultyListSet);

            if (IsSmartMitEnabled(Preset.WAR_Mitigation_Boss_ShakeItOff, rotationFlags) &&
                !JustUsed(Role.Reprisal, 10f) &&
                shakeItOffInMitigationContent &&
                ActionReady(ShakeItOff) &&
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
            !HasAnyStatusEffects([Buffs.ThrillOfBattle, Buffs.Damnation, Buffs.Vengeance, Buffs.BloodwhettingDefenseLong]) &&
            !JustUsed(Role.Reprisal, 10f) &&
            pressure.DangerRatio >= 1.5f)
        {
            actionID = ShakeItOff;
            TraceSmartMitigation(actionID, null, pressure, threat, isBoss, "trash_shake");
            return true;
        }

        return false;
    }

    private static List<MitigationOption> BuildWarBossMitigationOptions(RotationMode rotationFlags, WarThreatState threat)
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
                MitigationTier.Small));
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
                MitigationTier.Small));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_Boss_Rampart, rotationFlags) &&
            ActionReady(Role.Rampart) &&
            (rotationFlags.HasFlag(RotationMode.simple) ||
             ContentCheck.IsInConfiguredContent(WAR_Mitigation_Boss_Rampart_Difficulty, WAR_Boss_Mit_DifficultyListSet)))
        {
            options.Add(new MitigationOption(Role.Rampart, 0.20f, 0f, 0f, 90f, MitigationTier.Medium));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_Boss_Vengeance, rotationFlags) &&
            ActionReady(OriginalHook(Vengeance)) &&
            !HasAnyStatusEffects([Buffs.Vengeance, Buffs.Damnation]) &&
            (rotationFlags.HasFlag(RotationMode.simple) ||
             ContentCheck.IsInConfiguredContent(WAR_Mitigation_Boss_Vengeance_Difficulty, WAR_Boss_Mit_DifficultyListSet)))
        {
            var vengeanceAction = OriginalHook(Vengeance);
            var isDamnation = vengeanceAction != Vengeance;
            options.Add(new MitigationOption(
                vengeanceAction,
                isDamnation ? 0.40f : 0.30f,
                0f,
                0f,
                120f,
                MitigationTier.Large));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_Boss_ThrillOfBattle, rotationFlags) &&
            ActionReady(ThrillOfBattle) &&
            (rotationFlags.HasFlag(RotationMode.simple) ||
             ContentCheck.IsInConfiguredContent(WAR_Mitigation_Boss_ThrillOfBattle_Difficulty, WAR_Boss_Mit_DifficultyListSet)))
        {
            options.Add(new MitigationOption(ThrillOfBattle, 0f, 0f, 0.20f, 90f, MitigationTier.MaxHpBoost));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mit_Holmgang_Max, rotationFlags) &&
            ActionReady(Holmgang) &&
            (rotationFlags.HasFlag(RotationMode.simple) ||
             ContentCheck.IsInConfiguredContent(WAR_Mit_Holmgang_Max_Difficulty, WAR_Mit_Holmgang_Max_DifficultyListSet)))
        {
            options.Add(new MitigationOption(Holmgang, 1f, 0f, 0f, 240f, MitigationTier.Invuln));
        }

        return options;
    }

    private static List<MitigationOption> BuildWarNonBossMitigationOptions(RotationMode rotationFlags, WarThreatState threat)
    {
        var options = new List<MitigationOption>();
        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_RawIntuition, rotationFlags) &&
            ActionReady(OriginalHook(RawIntuition)) &&
            (threat.SustainedPressure || threat.MechanicSpikeFraction > 0f || enemyCount >= 3))
        {
            options.Add(new MitigationOption(
                OriginalHook(RawIntuition),
                0.10f,
                0.10f,
                0f,
                25f,
                MitigationTier.Small));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_Rampart, rotationFlags) &&
            Role.CanRampart())
        {
            options.Add(new MitigationOption(Role.Rampart, 0.20f, 0f, 0f, 90f, MitigationTier.Medium));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_ArmsLength, rotationFlags) &&
            ActionReady(Role.ArmsLength) &&
            enemyCount >= 3)
        {
            options.Add(new MitigationOption(Role.ArmsLength, 0.20f, 0f, 0f, 120f, MitigationTier.Medium));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_Vengeance, rotationFlags) &&
            ActionReady(OriginalHook(Vengeance)) &&
            !HasAnyStatusEffects([Buffs.Vengeance, Buffs.Damnation]) &&
            (enemyCount >= 5 || threat.MechanicSpikeFraction >= 0.25f))
        {
            var vengeanceAction = OriginalHook(Vengeance);
            var isDamnation = vengeanceAction != Vengeance;
            options.Add(new MitigationOption(
                vengeanceAction,
                isDamnation ? 0.40f : 0.30f,
                0f,
                0f,
                120f,
                MitigationTier.Large));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_ThrillOfBattle, rotationFlags) &&
            ActionReady(ThrillOfBattle))
        {
            options.Add(new MitigationOption(ThrillOfBattle, 0f, 0f, 0.20f, 90f, MitigationTier.MaxHpBoost));
        }

        if (IsSmartMitEnabled(Preset.WAR_Mitigation_NonBoss_Holmgang, rotationFlags) &&
            ActionReady(Holmgang))
        {
            options.Add(new MitigationOption(Holmgang, 1f, 0f, 0f, 240f, MitigationTier.Invuln));
        }

        return options;
    }

    private static ActiveMitigationState GetWarActiveMitigationState()
    {
        var reduction = 0f;
        var shield = 0f;
        var maxHpBonus = 0f;
        var invuln = false;

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

        if (HasAnyStatusEffects([Buffs.BloodwhettingDefenseLong, Buffs.BloodwhettingDefenseShort]))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.10f);

        if (HasStatusEffect(Buffs.BloodwhettingShield) && LocalPlayer is { } player && player.MaxHp > 0)
            shield += player.MaxHp * 0.10f;

        if (HasStatusEffect(Buffs.ThrillOfBattle))
            maxHpBonus += 0.20f;

        return new ActiveMitigationState(reduction, shield, maxHpBonus, invuln);
    }

    private static bool PassesWarSmartMitigationGuards(uint selectedActionId)
    {
        if (selectedActionId == OriginalHook(Vengeance))
            return !JustUsed(Role.Rampart, 20f);

        if (selectedActionId == Role.Rampart)
            return !JustUsed(OriginalHook(Vengeance), 15f);

        return true;
    }

    private static bool IsSmartMitEnabled(Preset preset, RotationMode rotationFlags) =>
        rotationFlags.HasFlag(RotationMode.simple) || CustomComboFunctions.IsEnabled(preset);

    private static void TraceSmartMitigation(
        uint selectedActionId,
        MitigationCoverageResult? coverage,
        PlayerPressureState pressure,
        WarThreatState threat,
        bool isBoss,
        string source)
    {
        if (!Service.Configuration.ParseLord5ExperimentalMode)
            return;

        var now = Environment.TickCount64;
        if (now < _nextParseLord5WarSmartMitTraceAt)
            return;

        _nextParseLord5WarSmartMitTraceAt = now + ParseLord5WarSmartMitTraceThrottleMs;

        var context = isBoss ? "boss" : "trash";
        if (coverage is { } result)
        {
            Svc.Log.Debug(
                "[ParseLord5][WAR_SmartMit] " +
                $"ctx={context} source={source} action={selectedActionId.ActionName()}({selectedActionId}) " +
                $"reason={result.Reason} budget={result.IncomingDamageBudget:F0} " +
                $"requiredR={result.RequiredReduction:F2} tb={threat.ConfirmedTankbuster} softTb={threat.SoftTankbuster} " +
                $"spike={threat.MechanicSpikeFraction:F2} netDps={pressure.NetDps:F0} ratio={pressure.DangerRatio:F2}");
            return;
        }

        Svc.Log.Debug(
            "[ParseLord5][WAR_SmartMit] " +
            $"ctx={context} source={source} action=none tb={threat.ConfirmedTankbuster} softTb={threat.SoftTankbuster} " +
            $"spike={threat.MechanicSpikeFraction:F2} netDps={pressure.NetDps:F0} ratio={pressure.DangerRatio:F2}");
    }
}
