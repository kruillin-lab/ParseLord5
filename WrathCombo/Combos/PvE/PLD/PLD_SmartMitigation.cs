using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Extensions;
using WrathCombo.Services;
using WrathCombo.Services.SmartMitigation;
using WrathCombo.Services.TankCooldownHelperIPC;
using static WrathCombo.Combos.PvE.PLD.Config;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class PLD
{
    private const long ParseLord5PldSmartMitTraceThrottleMs = 5_000;
    private static long _nextParseLord5PldSmartMitTraceAt;

    private static bool TrySmartBossMits(RotationMode rotationFlags, ref uint actionID) =>
        TrySmartMits(rotationFlags, isBoss: true, ref actionID);

    private static bool TrySmartNonBossMits(RotationMode rotationFlags, ref uint actionID) =>
        TrySmartMits(rotationFlags, isBoss: false, ref actionID);

    private static bool TrySmartMits(RotationMode rotationFlags, bool isBoss, ref uint actionID)
    {
        if (LocalPlayer is not { } player)
            return false;

        var isSimple = rotationFlags.HasFlag(RotationMode.Simple);
        var pressure = TankSmartMitigationThreat.GetPlayerPressure((uint)player.GameObjectId);

        actionID = 0;
        TraceSmartMitigation(0, null, pressure, default, isBoss, "enter");

        if (!isBoss && TrySmartPldNonBossPrepass(rotationFlags, ref actionID))
            return true;

        if (!isBoss && TrySmartPldNonBossEmergency(rotationFlags, ref actionID))
            return true;

        var threat = TankSmartMitigationThreat.Detect(isBoss, pressure);

        if (!TankSmartMitigationThreat.HasMitigationThreat(threat, isBoss, pressure))
        {
            TraceSmartMitigation(0, null, pressure, threat, isBoss, "no_threat");
            return false;
        }

        if (!isBoss && TrySelectPldTrashReprisalFirst(rotationFlags, pressure, threat, ref actionID))
            return true;

        if (TrySelectSmartPersonalMitigation(rotationFlags, isBoss, player.CurrentHp, player.MaxHp, pressure, threat, ref actionID))
            return true;

        return TrySelectSmartPartyMitigation(rotationFlags, isBoss, threat, pressure, ref actionID);
    }

    private static bool TrySmartPldNonBossPrepass(RotationMode rotationFlags, ref uint actionID)
    {
            if (!IsSmartMitEnabled(Preset.PLD_Mitigation_NonBoss_Sheltron, rotationFlags) ||
            !ActionReady(OriginalHook(Sheltron)) ||
            !CanPldWeave ||
            UsedPldMitigationThisGcd ||
            IsMoving() ||
            HasStatusEffect(Buffs.Sheltron) ||
            HasStatusEffect(Buffs.HallowedGround) ||
            Gauge.OathGauge < 50)
            return false;

        actionID = OriginalHook(Sheltron);
        return true;
    }

    private static bool TrySmartPldNonBossEmergency(RotationMode rotationFlags, ref uint actionID)
    {
        var threshold = rotationFlags.HasFlag(RotationMode.Simple)
            ? 20
            : PLD_Mitigation_NonBoss_HallowedGround_Health;

        if (IsSmartMitEnabled(Preset.PLD_Mitigation_NonBoss_HallowedGroundEmergency, rotationFlags) &&
            ActionReady(HallowedGround) &&
            PlayerHealthPercentageHp() <= threshold)
        {
            actionID = HallowedGround;
            return true;
        }

        return false;
    }

    private static bool TrySelectPldTrashReprisalFirst(
        RotationMode rotationFlags,
        PlayerPressureState pressure,
        TankThreatState threat,
        ref uint actionID)
    {
        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);
        var reprisalReady = ActionReady(Role.Reprisal) && Role.CanReprisal(checkTargetForDebuff: false);

        if (UsedPldMitigationThisGcd)
            return false;

        if (!TrashMitigationOrdering.ShouldPrioritizeTrashReprisalFirst(
                true,
                IsSmartMitEnabled(Preset.PLD_Mitigation_NonBoss_Reprisal, rotationFlags),
                reprisalReady,
                JustUsed(Role.Reprisal, 10f),
                enemyCount))
            return false;

        actionID = Role.Reprisal;
        TraceSmartMitigation(actionID, null, pressure, threat, false, "trash_reprisal_first");
        return true;
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
        if (!CanPldWeave)
            return false;

        if (UsedPldMitigationThisGcd)
            return false;

        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);
        var isSimple = rotationFlags.HasFlag(RotationMode.Simple);

        if (!isBoss && IsPldTrashMitigationThresholdBailout(isSimple) && !threat.ConfirmedTankbuster)
            return false;

        if (!isBoss && !pressure.TankCooldownEmergency &&
            ShouldSkipPldTrashMitigationStacking(threat, enemyCount))
            return false;

        if (isBoss &&
            !TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp) &&
            IsPldLongMitigationActive() &&
            !threat.ConfirmedTankbuster &&
            !threat.SoftTankbuster)
            return false;

        if (JustUsed(OriginalHook(Sentinel)) ||
            JustUsed(Bulwark) ||
            JustUsed(Role.Reprisal) ||
            JustUsed(Role.ArmsLength) ||
            JustUsed(Role.Rampart) ||
            JustUsed(HallowedGround))
            return false;

        var options = isBoss
            ? BuildPldBossMitigationOptions(rotationFlags, threat, pressure, currentHp, maxHp)
            : BuildPldNonBossMitigationOptions(rotationFlags, threat, pressure, currentHp, maxHp);

        var active = GetPldActiveMitigationState();
        options = FilterPldMitigationOptions(options, active, threat, currentHp, maxHp, pressure);

        if (options.Count == 0)
            return false;

        var preferHeavy = TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp);
        var request = new MitigationCoverageRequest(
            currentHp,
            maxHp,
            pressure.IncomingDps,
            pressure.IncomingHps,
            TankSmartMitigationThreat.ResolveMechanicSpikeFraction(threat, pressure),
            isBoss ? MitigationCoverageCalculator.DefaultHorizonSeconds : MitigationCoverageCalculator.TrashHorizonSeconds,
            isBoss ? MitigationCoverageCalculator.DefaultSafetyHpPercent : MitigationCoverageCalculator.TrashSafetyHpPercent,
            ConfirmedTankbuster: threat.ConfirmedTankbuster,
            SustainMultiplier: isBoss ? 1f : 1f + Math.Min(enemyCount, 5) * 0.06f,
            PreferHeavyMitigation: preferHeavy);

        var selected = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, active);
        var fallbackAction = 0u;
        if (selected is null &&
            !TrySelectPldMitigationFallback(options, ref fallbackAction) &&
            !TrySelectPldTchDangerFallback(options, threat, pressure, currentHp, maxHp, ref fallbackAction))
        {
            TraceSmartMitigation(0, null, pressure, threat, isBoss, "coverage_skip", currentHp, maxHp);
            return false;
        }

        if (selected is null)
        {
            if (fallbackAction == 0 || !PassesPldSmartMitigationGuards(fallbackAction))
                return false;

            actionID = fallbackAction;
            TraceSmartMitigation(actionID, null, pressure, threat, isBoss, "fallback", currentHp, maxHp);
            return true;
        }

        if (!PassesPldSmartMitigationGuards(selected!.Value.ActionId))
            return false;

        actionID = selected.Value.ActionId;
        TraceSmartMitigation(actionID, selected.Value, pressure, threat, isBoss, "personal", currentHp, maxHp);
        return true;
    }

    private static bool TrySelectSmartPartyMitigation(
        RotationMode rotationFlags,
        bool isBoss,
        TankThreatState threat,
        PlayerPressureState pressure,
        ref uint actionID)
    {
        if (!threat.Raidwide && (!isBoss || pressure.DangerRatio < 1.0f))
            return false;

        if (UsedPldMitigationThisGcd)
            return false;

        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);
        if (!isBoss && enemyCount < 3)
            return false;

        if (isBoss)
        {
            var reprisalInContent = rotationFlags.HasFlag(RotationMode.Simple) ||
                                    ContentCheck.IsInConfiguredContent(PLD_Mitigation_Boss_Reprisal_Difficulty, PLD_Boss_Mit_DifficultyListSet);

            if (IsSmartMitEnabled(Preset.PLD_Mitigation_Boss_Reprisal, rotationFlags) &&
                Role.CanReprisal(enemyCount: 1) &&
                reprisalInContent &&
                !JustUsed(DivineVeil, 10f) &&
                threat.Raidwide &&
                pressure.DangerRatio >= 1.0f)
            {
                actionID = Role.Reprisal;
                TraceSmartMitigation(actionID, null, pressure, threat, true, "party_reprisal");
                return true;
            }

            var veilInContent = rotationFlags.HasFlag(RotationMode.Simple) ||
                                ContentCheck.IsInConfiguredContent(PLD_Mitigation_Boss_DivineVeil_Difficulty, PLD_Boss_Mit_DifficultyListSet);

            if (IsSmartMitEnabled(Preset.PLD_Mitigation_Boss_DivineVeil, rotationFlags) &&
                veilInContent &&
                ActionReady(DivineVeil) &&
                !IsPldLongMitigationActive() &&
                !JustUsed(Role.Reprisal, 10f) &&
                pressure.DangerRatio >= 1.5f)
            {
                actionID = DivineVeil;
                TraceSmartMitigation(actionID, null, pressure, threat, true, "party_veil");
                return true;
            }

            return false;
        }

        if (IsSmartMitEnabled(Preset.PLD_Mitigation_NonBoss_Reprisal, rotationFlags) &&
            ActionReady(Role.Reprisal) &&
            enemyCount >= 3 &&
            !JustUsed(Role.Reprisal, 10f) &&
            pressure.DangerRatio >= 1.2f)
        {
            actionID = Role.Reprisal;
            TraceSmartMitigation(actionID, null, pressure, threat, false, "trash_reprisal");
            return true;
        }

        if (IsSmartMitEnabled(Preset.PLD_Mitigation_NonBoss_DivineVeil, rotationFlags) &&
            ActionReady(DivineVeil) &&
            !IsPldLongMitigationActive() &&
            !JustUsed(Role.Reprisal, 10f) &&
            PlayerHealthPercentageHp() <= (rotationFlags.HasFlag(RotationMode.Simple) ? 80 : PLD_Mitigation_NonBoss_DivineVeil_Health))
        {
            actionID = DivineVeil;
            TraceSmartMitigation(actionID, null, pressure, threat, false, "trash_veil");
            return true;
        }

        return false;
    }

    private static bool TrySelectPldTchDangerFallback(
        List<MitigationOption> options,
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp,
        ref uint actionID)
    {
        if (!pressure.FromTankCooldownHelper || !pressure.TankCooldownInDanger || options.Count == 0)
            return false;

        if (IsPldLongMitigationActive() &&
            !TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp))
            return false;

        if (TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp) &&
            TryPickHeavyFromOptions(options, ref actionID))
            return true;

        return TankMitigationSelection.TryPickLowestTier(
            options,
            IsPldLongMitigationActive(),
            IsPldLongMitigationAction,
            PassesPldSmartMitigationGuards,
            ref actionID);
    }

    private static bool TrySelectPldMitigationFallback(List<MitigationOption> options, ref uint actionID)
    {
        if (options.Count == 0 || IsPldLongMitigationActive() || !HasIncomingTankBusterEffect(out _))
            return false;

        return TankMitigationSelection.TryPickLowestTier(
            options,
            IsPldLongMitigationActive(),
            IsPldLongMitigationAction,
            PassesPldSmartMitigationGuards,
            ref actionID);
    }

    private static bool TryPickHeavyFromOptions(List<MitigationOption> options, ref uint actionID)
    {
        for (var i = 0; i < options.Count; i++)
        {
            if (!IsPldHeavyMitigationAction(options[i].ActionId))
                continue;

            if (!PassesPldSmartMitigationGuards(options[i].ActionId))
                continue;

            actionID = options[i].ActionId;
            return true;
        }

        return false;
    }

    private static List<MitigationOption> BuildPldBossMitigationOptions(
        RotationMode rotationFlags,
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        var options = new List<MitigationOption>();

        var rampartInContent = rotationFlags.HasFlag(RotationMode.Simple) ||
                               ContentCheck.IsInConfiguredContent(PLD_Mitigation_Boss_Rampart_Difficulty, PLD_Boss_Mit_DifficultyListSet);

        if (IsSmartMitEnabled(Preset.PLD_Mitigation_Boss_Rampart, rotationFlags) &&
            ActionReady(Role.Rampart) &&
            rampartInContent &&
            (threat.ConfirmedTankbuster || threat.SoftTankbuster || threat.SustainedPressure))
        {
            options.Add(new MitigationOption(Role.Rampart, 0.20f, 0f, 0f, 90f, MitigationTier.Medium));
        }

        var sentinelInContent = rotationFlags.HasFlag(RotationMode.Simple) ||
                                ContentCheck.IsInConfiguredContent(PLD_Mitigation_Boss_Sentinel_Difficulty, PLD_Boss_Mit_DifficultyListSet);

        if (IsSmartMitEnabled(Preset.PLD_Mitigation_Boss_Sentinel, rotationFlags) &&
            ActionReady(OriginalHook(Sentinel)) &&
            sentinelInContent &&
            TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp) &&
            !HasAnyStatusEffects([Buffs.Sentinel, Buffs.Guardian]))
        {
            var sentinelAction = OriginalHook(Sentinel);
            var isGuardian = sentinelAction != Sentinel;
            options.Add(new MitigationOption(
                sentinelAction,
                isGuardian ? 0.40f : 0.30f,
                0f,
                0f,
                120f,
                MitigationTier.Large));
        }

        if (IsSmartMitEnabled(Preset.PLD_Mitigation_Boss_Bulwark, rotationFlags) &&
            ActionReady(Bulwark) &&
            (rotationFlags.HasFlag(RotationMode.Simple) ||
             ContentCheck.IsInConfiguredContent(PLD_Mitigation_Boss_Bulwark_Difficulty, PLD_Boss_Mit_DifficultyListSet)) &&
            (threat.ConfirmedTankbuster || threat.SoftTankbuster))
        {
            options.Add(new MitigationOption(Bulwark, 0.20f, 0f, 0f, 90f, MitigationTier.Medium));
        }

        return options;
    }

    private static List<MitigationOption> BuildPldNonBossMitigationOptions(
        RotationMode rotationFlags,
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        var options = new List<MitigationOption>();
        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);

        if (IsSmartMitEnabled(Preset.PLD_Mitigation_NonBoss_Rampart, rotationFlags) &&
            Role.CanRampart() &&
            (enemyCount >= 3 || threat.SustainedPressure))
        {
            options.Add(new MitigationOption(Role.Rampart, 0.20f, 0f, 0f, 90f, MitigationTier.Medium));
        }

        if (IsSmartMitEnabled(Preset.PLD_Mitigation_NonBoss_ArmsLength, rotationFlags) &&
            ActionReady(Role.ArmsLength) &&
            enemyCount >= 3)
        {
            options.Add(new MitigationOption(Role.ArmsLength, 0.20f, 0f, 0f, 120f, MitigationTier.Medium));
        }

        if (IsSmartMitEnabled(Preset.PLD_Mitigation_NonBoss_Bulwark, rotationFlags) &&
            ActionReady(Bulwark) &&
            (enemyCount >= 3 || threat.SustainedPressure))
        {
            options.Add(new MitigationOption(Bulwark, 0.20f, 0f, 0f, 90f, MitigationTier.Medium));
        }

        if (IsSmartMitEnabled(Preset.PLD_Mitigation_NonBoss_Sentinel, rotationFlags) &&
            ActionReady(OriginalHook(Sentinel)) &&
            !HasAnyStatusEffects([Buffs.Sentinel, Buffs.Guardian]) &&
            TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp))
        {
            var sentinelAction = OriginalHook(Sentinel);
            var isGuardian = sentinelAction != Sentinel;
            options.Add(new MitigationOption(
                sentinelAction,
                isGuardian ? 0.40f : 0.30f,
                0f,
                0f,
                120f,
                MitigationTier.Large));
        }

        return options;
    }

    private static ActiveMitigationState GetPldActiveMitigationState()
    {
        var reduction = 0f;
        var shield = 0f;

        if (HasStatusEffect(Buffs.HallowedGround))
            return new ActiveMitigationState(1f, 0f, 0f, true);

        if (HasStatusEffect(Role.Buffs.Rampart))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.20f);

        if (HasStatusEffect(Role.Buffs.ArmsLength))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.20f);

        if (HasStatusEffect(Buffs.Bulwark))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.20f);

        if (HasStatusEffect(Buffs.Sentinel))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.30f);

        if (HasStatusEffect(Buffs.Guardian))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.40f);

        if (HasStatusEffect(Buffs.Sheltron) && LocalPlayer is { MaxHp: > 0 } player)
            shield += player.MaxHp * 0.10f;

        return new ActiveMitigationState(reduction, shield, 0f, false);
    }

    private static bool PassesPldSmartMitigationGuards(uint selectedActionId)
    {
        if (PerGcdActionCaps.ShouldHold(selectedActionId, IsPldMitigationAction))
            return false;

        if (selectedActionId == OriginalHook(Sentinel) || selectedActionId == Sentinel)
            return !JustUsed(Role.Rampart, 20f);

        if (selectedActionId == Role.Rampart)
            return !JustUsed(OriginalHook(Sentinel), 15f);

        return true;
    }

    private static bool UsedPldMitigationThisGcd =>
        PerGcdActionCaps.AnyUsed(IsPldMitigationAction);

    private static bool IsPldMitigationAction(uint action) =>
        action is Bulwark or DivineVeil or PassageOfArms or HallowedGround or Sheltron or HolySheltron or Guardian ||
        action == Sentinel ||
        action == OriginalHook(Sentinel) ||
        action == OriginalHook(Sheltron) ||
        action == Role.Rampart ||
        action == Role.Reprisal ||
        action == Role.ArmsLength;

    private static bool IsSmartMitEnabled(Preset preset, RotationMode rotationFlags) =>
        rotationFlags.HasFlag(RotationMode.Simple) || CustomComboFunctions.IsEnabled(preset);

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
        if (!ParseLord5Experiments.SmartMitigation)
            return;

        var now = Environment.TickCount64;
        if (now < _nextParseLord5PldSmartMitTraceAt)
            return;

        _nextParseLord5PldSmartMitTraceAt = now + ParseLord5PldSmartMitTraceThrottleMs;

        var context = isBoss ? "boss" : "trash";
        var actionLabel = selectedActionId == 0
            ? "none"
            : $"{selectedActionId.ActionName()}({selectedActionId})";

        Svc.Log.Debug(
            $"[ParseLord5][PLD_SmartMit] ctx={context} source={source} action={actionLabel} " +
            $"tb={threat.ConfirmedTankbuster} ratio={pressure.DangerRatio:F2} hp={currentHp}/{maxHp}");
    }
}
