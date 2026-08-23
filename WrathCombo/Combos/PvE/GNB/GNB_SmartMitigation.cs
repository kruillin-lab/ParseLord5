using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Extensions;
using WrathCombo.Services;
using WrathCombo.Services.SmartMitigation;
using static WrathCombo.Combos.PvE.GNB.Config;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

namespace WrathCombo.Combos.PvE;

internal partial class GNB
{
    private const long ParseLord5GnbSmartMitTraceThrottleMs = 5_000;
    private static long _nextParseLord5GnbSmartMitTraceAt;

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

        var threat = TankSmartMitigationThreat.Detect(isBoss, pressure);

        if (isBoss && TrySmartGnbBossAurora(rotationFlags, player.CurrentHp, player.MaxHp, pressure, threat, ref actionID))
            return true;

        if (!isBoss && TrySmartGnbNonBossPrepass(rotationFlags, ref actionID))
            return true;

        if (!isBoss && TrySmartGnbNonBossEmergency(rotationFlags, ref actionID))
            return true;

        if (!isBoss && TrySmartGnbNonBossAurora(rotationFlags, pressure, threat, ref actionID))
            return true;

        if (!TankSmartMitigationThreat.HasMitigationThreat(threat, isBoss, pressure))
        {
            TraceSmartMitigation(0, null, pressure, threat, isBoss, "no_threat");
            return false;
        }

        if (!isBoss && TrySelectGnbTrashReprisalFirst(rotationFlags, pressure, threat, ref actionID))
            return true;

        if (TrySelectSmartPersonalMitigation(rotationFlags, isBoss, player.CurrentHp, player.MaxHp, pressure, threat, ref actionID))
            return true;

        return TrySelectSmartPartyMitigation(rotationFlags, isBoss, threat, pressure, ref actionID);
    }

    private static bool TrySmartGnbBossAurora(
        RotationMode rotationFlags,
        uint currentHp,
        uint maxHp,
        PlayerPressureState pressure,
        TankThreatState threat,
        ref uint actionID)
    {
        if (!GnbAuroraSelection.ShouldUseBossSelfHeal(
                IsSmartMitEnabled(Preset.GNB_Mit_Advanced_Boss_Aurora, rotationFlags),
                CanWeave(),
                UsedGnbMitigationThisGcd,
                ActionReady(Aurora),
                currentHp,
                maxHp,
                rotationFlags.HasFlag(RotationMode.simple),
                GNB_Mit_Advanced_Boss_Aurora_Health,
                HasStatusEffect(Buffs.Aurora),
                JustUsed(Aurora),
                HasStatusEffect(Buffs.CatharsisOfCorundum)))
            return false;

        actionID = Aurora;
        TraceSmartMitigation(actionID, null, pressure, threat, true, "self_aurora", currentHp, maxHp);
        return true;
    }

    private static bool TrySmartGnbNonBossPrepass(RotationMode rotationFlags, ref uint actionID)
    {
        if (!IsSmartMitEnabled(Preset.GNB_Mit_Advanced_NonBoss_HeartOfStone, rotationFlags) ||
            !ActionReady(OriginalHook(HeartOfStone)) ||
            !CanWeave() ||
            UsedGnbMitigationThisGcd ||
            IsGnbCorundumActive() ||
            HasStatusEffect(Buffs.Superbolide))
            return false;

        actionID = OriginalHook(HeartOfStone);
        return true;
    }

    private static bool TrySmartGnbNonBossEmergency(RotationMode rotationFlags, ref uint actionID)
    {
        var threshold = rotationFlags.HasFlag(RotationMode.simple)
            ? 20
            : GNB_Mit_Advanced_NonBoss_SuperBolide_Health;

        if (IsSmartMitEnabled(Preset.GNB_Mit_Advanced_NonBoss_SuperBolideEmergency, rotationFlags) &&
            ActionReady(Superbolide) &&
            PlayerHealthPercentageHp() <= threshold)
        {
            actionID = Superbolide;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Trash Aurora self-heal. Restores the behaviour dropped when GNB moved to smart mit,
    ///     re-wiring the orphaned GNB_Mit_Advanced_NonBoss_Aurora preset (7705).
    /// </summary>
    private static bool TrySmartGnbNonBossAurora(
        RotationMode rotationFlags,
        PlayerPressureState pressure,
        TankThreatState threat,
        ref uint actionID)
    {
        if (!IsSmartMitEnabled(Preset.GNB_Mit_Advanced_NonBoss_Aurora, rotationFlags) ||
            !CanWeave() ||
            UsedGnbMitigationThisGcd ||
            !ActionReady(Aurora) ||
            HasStatusEffect(Buffs.Aurora) ||
            JustUsed(Aurora) ||
            HasStatusEffect(Buffs.Superbolide) ||
            HasStatusEffect(Buffs.CatharsisOfCorundum))
            return false;

        if (NumberOfEnemiesInRange(Role.Reprisal) < 3)
            return false;

        actionID = Aurora;
        TraceSmartMitigation(actionID, null, pressure, threat, false, "trash_aurora");
        return true;
    }

    private static bool TrySelectGnbTrashReprisalFirst(
        RotationMode rotationFlags,
        PlayerPressureState pressure,
        TankThreatState threat,
        ref uint actionID)
    {
        if (!CanWeave())
            return false;

        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);
        var reprisalReady = ActionReady(Role.Reprisal) && Role.CanReprisal(checkTargetForDebuff: false);

        if (UsedGnbMitigationThisGcd)
            return false;

        if (!TrashMitigationOrdering.ShouldPrioritizeTrashReprisalFirst(
                true,
                IsSmartMitEnabled(Preset.GNB_Mit_Advanced_NonBoss_Reprisal, rotationFlags),
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
        if (!CanWeave())
            return false;

        if (UsedGnbMitigationThisGcd)
            return false;

        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);
        var isSimple = rotationFlags.HasFlag(RotationMode.simple);

        if (!isBoss && IsGnbTrashMitigationThresholdBailout(isSimple) && !threat.ConfirmedTankbuster)
            return false;

        if (!isBoss && !pressure.TankCooldownEmergency &&
            ShouldSkipGnbTrashMitigationStacking(threat, enemyCount))
            return false;

        if (isBoss &&
            !TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp) &&
            IsGnbLongMitigationActive() &&
            !threat.ConfirmedTankbuster &&
            !threat.SoftTankbuster)
            return false;

        if (JustUsed(OriginalHook(Nebula)) ||
            JustUsed(Camouflage) ||
            JustUsed(Role.Reprisal) ||
            JustUsed(Role.ArmsLength) ||
            JustUsed(Role.Rampart) ||
            JustUsed(Superbolide))
            return false;

        var options = isBoss
            ? BuildGnbBossMitigationOptions(rotationFlags, threat, pressure, currentHp, maxHp)
            : BuildGnbNonBossMitigationOptions(rotationFlags, threat, pressure, currentHp, maxHp);

        var active = GetGnbActiveMitigationState();
        options = FilterGnbMitigationOptions(options, active, isBoss, threat, currentHp, maxHp, pressure);

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

        var selected = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, active, isBoss);
        var fallbackAction = 0u;
        if (selected is null &&
            !TrySelectGnbMitigationFallback(options, active, isBoss, threat, pressure, ref fallbackAction) &&
            !TrySelectGnbTchDangerFallback(options, active, isBoss, threat, pressure, currentHp, maxHp, ref fallbackAction))
        {
            TraceSmartMitigation(0, null, pressure, threat, isBoss, "coverage_skip", currentHp, maxHp);
            return false;
        }

        if (selected is null)
        {
            if (fallbackAction == 0 || !PassesGnbSmartMitigationGuards(fallbackAction))
                return false;

            actionID = fallbackAction;
            TraceSmartMitigation(actionID, null, pressure, threat, isBoss, "fallback", currentHp, maxHp);
            return true;
        }

        if (!PassesGnbSmartMitigationGuards(selected!.Value.ActionId))
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
        // Party mits are oGCDs too: without this the AoE path fires outside the weave
        // window and clips the GCD, while the personal path (weave-gated) is skipped.
        if (!CanWeave())
            return false;

        if (!threat.Raidwide && (!isBoss || pressure.DangerRatio < 1.0f))
            return false;

        if (UsedGnbMitigationThisGcd)
            return false;

        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);
        if (!isBoss && enemyCount < 3)
            return false;

        if (isBoss)
        {
            var reprisalInContent = rotationFlags.HasFlag(RotationMode.simple) ||
                                    ContentCheck.IsInConfiguredContent(GNB_Mit_Advanced_Boss_Reprisal_Difficulty, GNB_Boss_Mit_DifficultyListSet);

            if (IsSmartMitEnabled(Preset.GNB_Mit_Advanced_Boss_Reprisal, rotationFlags) &&
                Role.CanReprisal(enemyCount: 1) &&
                reprisalInContent &&
                !JustUsed(HeartOfLight, 10f) &&
                threat.Raidwide &&
                pressure.DangerRatio >= 1.0f)
            {
                actionID = Role.Reprisal;
                TraceSmartMitigation(actionID, null, pressure, threat, true, "party_reprisal");
                return true;
            }

            var holInContent = rotationFlags.HasFlag(RotationMode.simple) ||
                               ContentCheck.IsInConfiguredContent(GNB_Mit_Advanced_Boss_HeartOfLight_Difficulty, GNB_Boss_Mit_DifficultyListSet);

            if (IsSmartMitEnabled(Preset.GNB_Mit_Advanced_Boss_HeartOfLight, rotationFlags) &&
                holInContent &&
                ActionReady(HeartOfLight) &&
                !IsGnbLongMitigationActive() &&
                !JustUsed(Role.Reprisal, 10f) &&
                pressure.DangerRatio >= 1.5f)
            {
                actionID = HeartOfLight;
                TraceSmartMitigation(actionID, null, pressure, threat, true, "party_hol");
                return true;
            }

            return false;
        }

        if (IsSmartMitEnabled(Preset.GNB_Mit_Advanced_NonBoss_Reprisal, rotationFlags) &&
            ActionReady(Role.Reprisal) &&
            enemyCount >= 3 &&
            !JustUsed(Role.Reprisal, 10f) &&
            pressure.DangerRatio >= 1.2f)
        {
            actionID = Role.Reprisal;
            TraceSmartMitigation(actionID, null, pressure, threat, false, "trash_reprisal");
            return true;
        }

        if (IsSmartMitEnabled(Preset.GNB_Mit_Advanced_NonBoss_HeartOfLight, rotationFlags) &&
            ActionReady(HeartOfLight) &&
            !IsGnbLongMitigationActive() &&
            !JustUsed(Role.Reprisal, 10f) &&
            (enemyCount >= 5 || pressure.DangerRatio >= 1.2f))
        {
            actionID = HeartOfLight;
            TraceSmartMitigation(actionID, null, pressure, threat, false, "trash_hol");
            return true;
        }

        return false;
    }

    private static bool TrySelectGnbTchDangerFallback(
        List<MitigationOption> options,
        ActiveMitigationState active,
        bool isBoss,
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp,
        ref uint actionID)
    {
        if (!pressure.FromTankCooldownHelper || !pressure.TankCooldownInDanger || options.Count == 0)
            return false;

        if (TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp) &&
            TryPickHeavyFromOptions(options, ref actionID))
            return true;

        return TankMitigationSelection.TryPickLowestTier(
            options,
            active,
            isBoss,
            PassesGnbSmartMitigationGuards,
            ref actionID);
    }

    private static bool TrySelectGnbMitigationFallback(
        List<MitigationOption> options,
        ActiveMitigationState active,
        bool isBoss,
        TankThreatState threat,
        PlayerPressureState pressure,
        ref uint actionID)
    {
        if (options.Count == 0)
            return false;

        return TankMitigationSelection.TryPickTankMitigationFallback(
            options,
            active,
            isBoss,
            HasIncomingTankBusterEffect(out _),
            threat.SoftTankbuster,
            threat.SustainedPressure,
            pressure.FromTankCooldownHelper,
            PassesGnbSmartMitigationGuards,
            ref actionID);
    }

    private static bool TryPickHeavyFromOptions(List<MitigationOption> options, ref uint actionID)
    {
        for (var i = 0; i < options.Count; i++)
        {
            if (!IsGnbHeavyMitigationAction(options[i].ActionId))
                continue;

            if (!PassesGnbSmartMitigationGuards(options[i].ActionId))
                continue;

            actionID = options[i].ActionId;
            return true;
        }

        return false;
    }

    private static List<MitigationOption> BuildGnbBossMitigationOptions(
        RotationMode rotationFlags,
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        var options = new List<MitigationOption>();

        var rampartInContent = rotationFlags.HasFlag(RotationMode.simple) ||
                               ContentCheck.IsInConfiguredContent(GNB_Mit_Advanced_Boss_Rampart_Difficulty, GNB_Boss_Mit_DifficultyListSet);

        if (IsSmartMitEnabled(Preset.GNB_Mit_Advanced_Boss_Rampart, rotationFlags) &&
            ActionReady(Role.Rampart) &&
            rampartInContent &&
            (threat.ConfirmedTankbuster || threat.SoftTankbuster || threat.SustainedPressure))
        {
            options.Add(new MitigationOption(Role.Rampart, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Long));
        }

        var nebulaInContent = rotationFlags.HasFlag(RotationMode.simple) ||
                              ContentCheck.IsInConfiguredContent(GNB_Mit_Advanced_Boss_Nebula_Difficulty, GNB_Boss_Mit_DifficultyListSet);

        if (IsSmartMitEnabled(Preset.GNB_Mit_Advanced_Boss_Nebula, rotationFlags) &&
            ActionReady(OriginalHook(Nebula)) &&
            nebulaInContent &&
            TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp) &&
            !HasAnyStatusEffects([Buffs.Nebula, Buffs.GreatNebula]))
        {
            var nebulaAction = OriginalHook(Nebula);
            var isGreat = nebulaAction != Nebula;
            options.Add(new MitigationOption(
                nebulaAction,
                isGreat ? 0.40f : 0.30f,
                0f,
                0f,
                120f,
                MitigationTier.Large,
                MitigationPool.Long));
        }

        AddGnbBossCorundumOption(options, rotationFlags, threat, currentHp, maxHp);

        var camoInContent = rotationFlags.HasFlag(RotationMode.simple) ||
                            ContentCheck.IsInConfiguredContent(GNB_Mit_Advanced_Boss_Camouflage_Difficulty, GNB_Boss_Mit_DifficultyListSet);

        if (IsSmartMitEnabled(Preset.GNB_Mit_Advanced_Boss_Camouflage, rotationFlags) &&
            ActionReady(Camouflage) &&
            camoInContent &&
            (threat.ConfirmedTankbuster || threat.SoftTankbuster))
        {
            options.Add(new MitigationOption(Camouflage, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Short));
        }

        return options;
    }

    /// <summary>
    ///     Boss Heart of Stone / Heart of Corundum. Exempt pool: a 25s personal shield+DR that
    ///     always stacks, so it keeps a usable option available while a long CD is running.
    ///     Wires the previously-orphaned OnCD (7716) and TankBuster (7717) presets.
    /// </summary>
    private static void AddGnbBossCorundumOption(
        List<MitigationOption> options,
        RotationMode rotationFlags,
        TankThreatState threat,
        uint currentHp,
        uint maxHp)
    {
        var corundum = OriginalHook(HeartOfStone);

        if (!ActionReady(corundum) || IsGnbCorundumActive() || maxHp == 0)
            return;

        var onCdInContent = rotationFlags.HasFlag(RotationMode.simple) ||
                            ContentCheck.IsInConfiguredContent(GNB_Mit_Advanced_Boss_HeartOfStone_OnCD_Difficulty, GNB_Boss_Mit_DifficultyListSet);

        var tankBusterInContent = rotationFlags.HasFlag(RotationMode.simple) ||
                                  ContentCheck.IsInConfiguredContent(GNB_Mit_Advanced_Boss_HeartOfStone_TankBuster_Difficulty, GNB_Boss_Mit_DifficultyListSet);

        var healthThreshold = rotationFlags.HasFlag(RotationMode.simple)
            ? 80
            : GNB_Mit_Advanced_Boss_HeartOfStone_Health;

        var onCd = IsSmartMitEnabled(Preset.GNB_Mit_Advanced_Boss_HeartOfStone_OnCD, rotationFlags) &&
                   onCdInContent &&
                   (ulong)currentHp * 100 <= (ulong)maxHp * (uint)healthThreshold;

        var onTankBuster = IsSmartMitEnabled(Preset.GNB_Mit_Advanced_Boss_HeartOfStone_TankBuster, rotationFlags) &&
                           tankBusterInContent &&
                           (threat.ConfirmedTankbuster || threat.SoftTankbuster);

        if (!onCd && !onTankBuster)
            return;

        options.Add(new MitigationOption(
            corundum,
            0.15f,
            maxHp * 0.10f,
            0f,
            25f,
            MitigationTier.Small,
            MitigationPool.Exempt));
    }

    private static List<MitigationOption> BuildGnbNonBossMitigationOptions(
        RotationMode rotationFlags,
        TankThreatState threat,
        PlayerPressureState pressure,
        uint currentHp,
        uint maxHp)
    {
        var options = new List<MitigationOption>();
        var enemyCount = NumberOfEnemiesInRange(Role.Reprisal);

        if (IsSmartMitEnabled(Preset.GNB_Mit_Advanced_NonBoss_Rampart, rotationFlags) &&
            Role.CanRampart() &&
            (enemyCount >= 3 || threat.SustainedPressure))
        {
            options.Add(new MitigationOption(Role.Rampart, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Long));
        }

        if (IsSmartMitEnabled(Preset.GNB_Mit_Advanced_NonBoss_ArmsLength, rotationFlags) &&
            ActionReady(Role.ArmsLength) &&
            enemyCount >= 3)
        {
            options.Add(new MitigationOption(Role.ArmsLength, 0.20f, 0f, 0f, 120f, MitigationTier.Medium, MitigationPool.TrashOnly));
        }

        if (IsSmartMitEnabled(Preset.GNB_Mit_Advanced_NonBoss_Camouflage, rotationFlags) &&
            ActionReady(Camouflage) &&
            (enemyCount >= 3 || threat.SustainedPressure))
        {
            options.Add(new MitigationOption(Camouflage, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Short));
        }

        if (IsSmartMitEnabled(Preset.GNB_Mit_Advanced_NonBoss_Nebula, rotationFlags) &&
            ActionReady(OriginalHook(Nebula)) &&
            !HasAnyStatusEffects([Buffs.Nebula, Buffs.GreatNebula]) &&
            TankSmartMitigationThreat.ShouldOfferHeavyMitigation(threat, pressure, currentHp, maxHp))
        {
            var nebulaAction = OriginalHook(Nebula);
            var isGreat = nebulaAction != Nebula;
            options.Add(new MitigationOption(
                nebulaAction,
                isGreat ? 0.40f : 0.30f,
                0f,
                0f,
                120f,
                MitigationTier.Large,
                MitigationPool.Long));
        }

        return options;
    }

    private static ActiveMitigationState GetGnbActiveMitigationState()
    {
        var reduction = 0f;
        var shield = 0f;
        var longPoolActive = IsGnbLongMitigationActive();
        var shortPoolActive = IsGnbShortMitigationActive();

        if (HasStatusEffect(Buffs.Superbolide))
            return new ActiveMitigationState(1f, 0f, 0f, true, longPoolActive, shortPoolActive);

        if (HasStatusEffect(Role.Buffs.Rampart))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.20f);

        if (HasStatusEffect(Role.Buffs.ArmsLength))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.20f);

        if (HasStatusEffect(Buffs.Camouflage))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.20f);

        if (HasStatusEffect(Buffs.Nebula))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.30f);

        if (HasStatusEffect(Buffs.GreatNebula))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.40f);

        // Heart of Corundum's damage-reduction window (Clarity of Corundum), previously uncounted.
        if (HasStatusEffect(Buffs.ClarityOfCorundum))
            reduction = MitigationCoverageCalculator.CombineReduction(reduction, 0.15f);

        if (HasAnyStatusEffects([Buffs.HeartOfStone, Buffs.HeartOfCorundum]) && LocalPlayer is { MaxHp: > 0 } player)
            shield += player.MaxHp * 0.10f;

        return new ActiveMitigationState(reduction, shield, 0f, false, longPoolActive, shortPoolActive);
    }

    private static bool PassesGnbSmartMitigationGuards(uint selectedActionId)
    {
        if (PerGcdActionCaps.ShouldHold(selectedActionId, IsGnbMitigationAction))
            return false;

        if (IsGnbHeavyMitigationAction(selectedActionId))
            return !JustUsed(Role.Rampart, 20f);

        if (selectedActionId == Role.Rampart)
            return !JustUsed(OriginalHook(Nebula), 15f);

        return true;
    }

    private static bool UsedGnbMitigationThisGcd =>
        PerGcdActionCaps.AnyUsed(IsGnbMitigationAction);

    private static bool IsGnbMitigationAction(uint action) =>
        action is Aurora or Camouflage or HeartOfLight or Superbolide or HeartOfStone or HeartOfCorundum or Nebula or GreatNebula ||
        action == OriginalHook(HeartOfStone) ||
        action == OriginalHook(Nebula) ||
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
        if (now < _nextParseLord5GnbSmartMitTraceAt)
            return;

        _nextParseLord5GnbSmartMitTraceAt = now + ParseLord5GnbSmartMitTraceThrottleMs;

        var actionLabel = selectedActionId == 0
            ? "none"
            : $"{selectedActionId.ActionName()}({selectedActionId})";

        Svc.Log.Debug(
            $"[ParseLord5][GNB_SmartMit] ctx={(isBoss ? "boss" : "trash")} source={source} action={actionLabel} ratio={pressure.DangerRatio:F2}");
    }
}
