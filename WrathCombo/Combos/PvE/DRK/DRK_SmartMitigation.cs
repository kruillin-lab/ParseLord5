using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Extensions;
using WrathCombo.Services;
using WrathCombo.Services.SmartMitigation;
using static WrathCombo.Combos.PvE.DRK.Config;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class DRK
{
    private const long ParseLord5DrkSmartMitTraceThrottleMs = 5_000;
    private static long _nextParseLord5DrkSmartMitTraceAt;

    private static bool TrySmartBossMits(Combo flags, ref uint actionID) =>
        TrySmartMits(flags, isBoss: true, ref actionID);

    private static bool TrySmartNonBossMits(Combo flags, ref uint actionID) =>
        TrySmartMits(flags, isBoss: false, ref actionID);

    private static bool TrySmartMits(Combo flags, bool isBoss, ref uint actionID)
    {
        if (LocalPlayer is not { } player || !InCombat())
            return false;

        if (isBoss)
        {
            if (!CanWeave || !InBossEncounter() || !IsSmartMitEnabled(Preset.DRK_Mitigation_Boss, flags))
                return false;
        }
        else if (!IsSmartMitEnabled(Preset.DRK_Mitigation_NonBoss, flags) ||
                 InBossEncounter() ||
                 (CombatEngageDuration().TotalSeconds <= 15 && IsMoving()))
        {
            return false;
        }

        var pressure = TankSmartMitigationThreat.GetPlayerPressure((uint)player.GameObjectId);

        // Invoke passes the combo anchor (HardSlash/Unleash); must not treat as a fallback pick.
        actionID = 0;
        TraceSmartMitigation(0, null, default, default, isBoss, "enter");

        if (!isBoss && TrySmartDrkNonBossPrepass(flags, ref actionID))
            return true;

        if (!isBoss && TrySmartDrkNonBossEmergency(flags, ref actionID))
            return true;

        var threat = TankSmartMitigationThreat.Detect(isBoss, pressure);

        if (!TankSmartMitigationThreat.HasMitigationThreat(threat, isBoss, pressure))
        {
            TraceSmartMitigation(0, null, pressure, threat, isBoss, "no_threat");
            return false;
        }

        if (!isBoss && TrySelectDrkTrashReprisalFirst(flags, pressure, threat, ref actionID))
            return true;

        if (TrySelectSmartPersonalMitigation(flags, isBoss, player.CurrentHp, player.MaxHp, pressure, threat, ref actionID))
            return true;

        return TrySelectSmartPartyMitigation(flags, isBoss, threat, pressure, ref actionID);
    }

    private static bool TrySmartDrkNonBossPrepass(Combo flags, ref uint actionID)
    {
        if (!IsSmartMitEnabled(Preset.DRK_Mitigation_NonBoss_BlackestNight, flags) ||
            !ActionReady(BlackestNight) ||
            !CanWeave ||
            UsedDrkMitigationThisGcd)
            return false;

        if (HasAnyTBN && GetCooldownRemainingTime(LivingShadow) >= 30)
            return false;

        actionID = BlackestNight;
        return true;
    }

    private static bool TrySmartDrkNonBossEmergency(Combo flags, ref uint actionID)
    {
        var threshold = flags.HasFlag(Combo.Simple) ? 10 : DRK_Mit_NonBoss_LivingDead_Health;

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_NonBoss_LivingDead, flags) &&
            ActionReady(LivingDead) &&
            PlayerHealthPercentageHp() <= threshold)
        {
            actionID = LivingDead;
            return true;
        }

        return false;
    }

    private static bool TrySelectDrkTrashReprisalFirst(
        Combo flags,
        PlayerPressureState pressure,
        TankThreatState threat,
        ref uint actionID)
    {
        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);
        var reprisalReady = ActionReady(Role.Reprisal) && Role.CanReprisal(checkTargetForDebuff: false);

        if (UsedDrkMitigationThisGcd)
            return false;

        if (!TrashMitigationOrdering.ShouldPrioritizeTrashReprisalFirst(
                true,
                IsSmartMitEnabled(Preset.DRK_Mitigation_NonBoss_Reprisal, flags),
                reprisalReady,
                JustUsed(Role.Reprisal, 10f),
                enemyCount))
            return false;

        actionID = Role.Reprisal;
        TraceSmartMitigation(actionID, null, pressure, threat, false, "trash_reprisal_first");
        return true;
    }

    private static bool TrySelectSmartPersonalMitigation(
        Combo flags,
        bool isBoss,
        uint currentHp,
        uint maxHp,
        PlayerPressureState pressure,
        TankThreatState threat,
        ref uint actionID)
    {
        if (!CanWeave || JustUsedMitigation || UsedDrkMitigationThisGcd)
            return false;

        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);
        var isSimple = flags.HasFlag(Combo.Simple);

        if (!isBoss && IsDrkTrashMitigationThresholdBailout(isSimple) && !threat.ConfirmedTankbuster)
            return false;

        if (!isBoss && !pressure.TankCooldownEmergency &&
            ShouldSkipDrkTrashMitigationStacking(threat, enemyCount))
            return false;

        if (isBoss &&
            !TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp) &&
            IsDrkLongMitigationActive() &&
            !threat.ConfirmedTankbuster &&
            !threat.SoftTankbuster)
            return false;

        var options = isBoss
            ? BuildDrkBossMitigationOptions(flags, threat, pressure, currentHp, maxHp)
            : BuildDrkNonBossMitigationOptions(flags, threat, pressure, currentHp, maxHp);

        var active = GetDrkActiveMitigationState();
        options = FilterDrkMitigationOptions(options, active, threat, currentHp, maxHp, pressure);

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
            !TrySelectDrkMitigationFallback(options, ref fallbackAction) &&
            !TrySelectDrkTchDangerFallback(options, threat, pressure, currentHp, maxHp, ref fallbackAction))
        {
            TraceSmartMitigation(0, null, pressure, threat, isBoss, "coverage_skip", currentHp, maxHp);
            return false;
        }

        if (selected is null)
        {
            if (fallbackAction == 0 || !PassesDrkSmartMitigationGuards(fallbackAction))
                return false;

            actionID = fallbackAction;
            TraceSmartMitigation(actionID, null, pressure, threat, isBoss, "fallback", currentHp, maxHp);
            return true;
        }

        if (!PassesDrkSmartMitigationGuards(selected!.Value.ActionId))
            return false;

        actionID = selected.Value.ActionId;
        TraceSmartMitigation(actionID, selected.Value, pressure, threat, isBoss, "personal", currentHp, maxHp);
        return true;
    }

    private static bool TrySelectSmartPartyMitigation(
        Combo flags,
        bool isBoss,
        TankThreatState threat,
        PlayerPressureState pressure,
        ref uint actionID)
    {
        if (!threat.Raidwide && (!isBoss || pressure.DangerRatio < 1.0f))
            return false;

        if (UsedDrkMitigationThisGcd)
            return false;

        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);
        if (!isBoss && enemyCount < 3)
            return false;

        if (isBoss)
        {
            var reprisalInContent = flags.HasFlag(Combo.Simple) ||
                                    ContentCheck.IsInConfiguredContent(
                                        DRK_Mit_Boss_Reprisal_Difficulty,
                                        DRK_Boss_Mit_DifficultyListSet);

            if (IsSmartMitEnabled(Preset.DRK_Mitigation_Boss_Reprisal, flags) &&
                reprisalInContent &&
                !JustUsed(DarkMissionary, 10f) &&
                Role.CanReprisal(enemyCount: 1) &&
                threat.Raidwide &&
                pressure.DangerRatio >= 1.0f)
            {
                actionID = Role.Reprisal;
                TraceSmartMitigation(actionID, null, pressure, threat, true, "party_reprisal");
                return true;
            }

            var dmInContent = flags.HasFlag(Combo.Simple) ||
                              ContentCheck.IsInConfiguredContent(
                                  DRK_Mit_Boss_DarkMissionary_Difficulty,
                                  DRK_Boss_Mit_DifficultyListSet);

            if (IsSmartMitEnabled(Preset.DRK_Mitigation_Boss_DarkMissionary, flags) &&
                dmInContent &&
                ActionReady(DarkMissionary) &&
                !IsDrkLongMitigationActive() &&
                !JustUsed(Role.Reprisal, 10f) &&
                pressure.DangerRatio >= 1.5f)
            {
                actionID = DarkMissionary;
                TraceSmartMitigation(actionID, null, pressure, threat, true, "party_dm");
                return true;
            }

            return false;
        }

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_NonBoss_Reprisal, flags) &&
            ActionReady(Role.Reprisal) &&
            enemyCount >= 3 &&
            !JustUsed(Role.Reprisal, 10f) &&
            pressure.DangerRatio >= 1.2f)
        {
            actionID = Role.Reprisal;
            TraceSmartMitigation(actionID, null, pressure, threat, false, "trash_reprisal");
            return true;
        }

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_NonBoss_DarkMissionary, flags) &&
            ActionReady(DarkMissionary) &&
            !IsDrkLongMitigationActive() &&
            !JustUsed(OriginalHook(ShadowWall), 15f) &&
            (enemyCount >= 5 || pressure.DangerRatio >= 1.2f))
        {
            actionID = DarkMissionary;
            TraceSmartMitigation(actionID, null, pressure, threat, false, "trash_dm");
            return true;
        }

        return false;
    }

    private static bool TrySelectDrkTchDangerFallback(
        List<MitigationOption> options,
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp,
        ref uint actionID)
    {
        if (!pressure.FromTankCooldownHelper || !pressure.TankCooldownInDanger || options.Count == 0)
            return false;

        if (IsDrkLongMitigationActive() &&
            !TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp))
            return false;

        if (TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp) &&
            TryPickHeavyFromOptions(options, ref actionID))
            return true;

        return TankMitigationSelection.TryPickLowestTier(
            options,
            IsDrkLongMitigationActive(),
            IsDrkLongMitigationAction,
            PassesDrkSmartMitigationGuards,
            ref actionID);
    }

    private static bool TrySelectDrkMitigationFallback(List<MitigationOption> options, ref uint actionID)
    {
        if (options.Count == 0 || IsDrkLongMitigationActive() || !HasIncomingTankBusterEffect(out _))
            return false;

        return TankMitigationSelection.TryPickLowestTier(
            options,
            IsDrkLongMitigationActive(),
            IsDrkLongMitigationAction,
            PassesDrkSmartMitigationGuards,
            ref actionID);
    }

    private static bool TryPickHeavyFromOptions(List<MitigationOption> options, ref uint actionID)
    {
        for (var i = 0; i < options.Count; i++)
        {
            if (!IsDrkHeavyMitigationAction(options[i].ActionId))
                continue;

            if (!PassesDrkSmartMitigationGuards(options[i].ActionId))
                continue;

            actionID = options[i].ActionId;
            return true;
        }

        return false;
    }

    private static List<MitigationOption> BuildDrkBossMitigationOptions(
        Combo flags,
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        var options = new List<MitigationOption>();

        var rampartInContent = flags.HasFlag(Combo.Simple) ||
                               ContentCheck.IsInConfiguredContent(
                                   DRK_Mit_Boss_Rampart_Difficulty,
                                   DRK_Boss_Mit_DifficultyListSet);

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_Boss_Rampart, flags) &&
            ActionReady(Role.Rampart) &&
            rampartInContent &&
            (threat.ConfirmedTankbuster || threat.SoftTankbuster || threat.SustainedPressure))
        {
            options.Add(new MitigationOption(Role.Rampart, 0.20f, 0f, 0f, 90f, MitigationTier.Medium));
        }

        var wallInContent = flags.HasFlag(Combo.Simple) ||
                            ContentCheck.IsInConfiguredContent(
                                DRK_Mit_Boss_ShadowWall_Difficulty,
                                DRK_Boss_Mit_DifficultyListSet);

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_Boss_ShadowWall, flags) &&
            ActionReady(OriginalHook(ShadowWall)) &&
            wallInContent &&
            TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp) &&
            !HasAnyStatusEffects([Buffs.ShadowWall, Buffs.ShadowedVigil]))
        {
            var wallAction = OriginalHook(ShadowWall);
            var isVigil = wallAction != ShadowWall;
            options.Add(new MitigationOption(
                wallAction,
                isVigil ? 0.40f : 0.30f,
                0f,
                0f,
                120f,
                MitigationTier.Large));
        }

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_Boss_DarkMind, flags) &&
            ActionReady(DarkMind) &&
            (flags.HasFlag(Combo.Simple) ||
             ContentCheck.IsInConfiguredContent(DRK_Mit_Boss_DarkMind_Difficulty, DRK_Boss_Mit_DifficultyListSet)) &&
            (threat.ConfirmedTankbuster || threat.SoftTankbuster))
        {
            options.Add(new MitigationOption(DarkMind, 0.20f, 0f, 0f, 60f, MitigationTier.Medium));
        }

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_Boss_Oblation, flags) &&
            ActionReady(Oblation) &&
            (flags.HasFlag(Combo.Simple) ||
             ContentCheck.IsInConfiguredContent(DRK_Mit_Boss_Oblation_TankBuster_Difficulty, DRK_Boss_Mit_DifficultyListSet)) &&
            threat.ConfirmedTankbuster)
        {
            options.Add(new MitigationOption(Oblation, 0.10f, 0f, 0f, 60f, MitigationTier.Small));
        }

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_Boss_BlackestNight_TB, flags) &&
            ActionReady(BlackestNight) &&
            threat.ConfirmedTankbuster)
        {
            options.Add(new MitigationOption(BlackestNight, 0f, 0.25f, 0f, 15f, MitigationTier.Small));
        }

        return options;
    }

    private static List<MitigationOption> BuildDrkNonBossMitigationOptions(
        Combo flags,
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        var options = new List<MitigationOption>();
        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_NonBoss_DarkMind, flags) &&
            ActionReady(DarkMind) &&
            (enemyCount >= 3 || threat.SustainedPressure))
        {
            options.Add(new MitigationOption(DarkMind, 0.20f, 0f, 0f, 60f, MitigationTier.Medium));
        }

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_NonBoss_Rampart, flags) &&
            Role.CanRampart() &&
            (enemyCount >= 3 || threat.SustainedPressure))
        {
            options.Add(new MitigationOption(Role.Rampart, 0.20f, 0f, 0f, 90f, MitigationTier.Medium));
        }

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_NonBoss_ArmsLength, flags) &&
            ActionReady(Role.ArmsLength) &&
            enemyCount >= 3)
        {
            options.Add(new MitigationOption(Role.ArmsLength, 0.20f, 0f, 0f, 120f, MitigationTier.Medium));
        }

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_NonBoss_ShadowWall, flags) &&
            ActionReady(OriginalHook(ShadowWall)) &&
            !HasAnyStatusEffects([Buffs.ShadowWall, Buffs.ShadowedVigil]) &&
            TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp))
        {
            var wallAction = OriginalHook(ShadowWall);
            var isVigil = wallAction != ShadowWall;
            options.Add(new MitigationOption(
                wallAction,
                isVigil ? 0.40f : 0.30f,
                0f,
                0f,
                120f,
                MitigationTier.Large));
        }

        if (IsSmartMitEnabled(Preset.DRK_Mitigation_NonBoss_Oblation, flags) &&
            ActionReady(Oblation) &&
            enemyCount >= 4)
        {
            options.Add(new MitigationOption(Oblation, 0.10f, 0f, 0f, 60f, MitigationTier.Small));
        }

        return options;
    }

    private static ActiveMitigationState GetDrkActiveMitigationState()
    {
        var reduction = 0f;
        var shield = 0f;

        if (HasAnyStatusEffects([Buffs.LivingDead, Buffs.WalkingDead, Buffs.UndeadRebirth]))
            return new ActiveMitigationState(1f, 0f, 0f, true);

        if (HasStatusEffect(Role.Buffs.Rampart))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.20f);

        if (HasStatusEffect(Role.Buffs.ArmsLength))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.20f);

        if (HasStatusEffect(Buffs.DarkMind))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.20f);

        if (HasStatusEffect(Buffs.ShadowWall))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.30f);

        if (HasStatusEffect(Buffs.ShadowedVigil))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.40f);

        if (HasOwnTBN && LocalPlayer is { MaxHp: > 0 } player)
            shield += player.MaxHp * 0.25f;

        return new ActiveMitigationState(reduction, shield, 0f, false);
    }

    private static bool PassesDrkSmartMitigationGuards(uint selectedActionId)
    {
        if (PerGcdActionCaps.ShouldHold(selectedActionId, IsDrkMitigationAction))
            return false;

        if (IsDrkHeavyMitigationAction(selectedActionId))
            return !JustUsed(Role.Rampart, 20f);

        if (selectedActionId == Role.Rampart)
            return !JustUsed(OriginalHook(ShadowWall), 15f);

        return true;
    }

    private static bool UsedDrkMitigationThisGcd =>
        PerGcdActionCaps.AnyUsed(IsDrkMitigationAction);

    private static bool IsDrkMitigationAction(uint action) =>
        action is DarkMind or LivingDead or DarkMissionary or BlackestNight or Oblation or ShadowedVigil or ShadowWall ||
        action == OriginalHook(ShadowWall) ||
        action == Role.Rampart ||
        action == Role.Reprisal ||
        action == Role.ArmsLength;

    private static bool IsSmartMitEnabled(Preset preset, Combo flags) =>
        flags.HasFlag(Combo.Simple) || CustomComboFunctions.IsEnabled(preset);

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
        if (now < _nextParseLord5DrkSmartMitTraceAt)
            return;

        _nextParseLord5DrkSmartMitTraceAt = now + ParseLord5DrkSmartMitTraceThrottleMs;

        var actionLabel = selectedActionId == 0
            ? "none"
            : $"{selectedActionId.ActionName()}({selectedActionId})";

        Svc.Log.Debug(
            $"[ParseLord5][DRK_SmartMit] ctx={(isBoss ? "boss" : "trash")} source={source} action={actionLabel} ratio={pressure.DangerRatio:F2}");
    }
}
