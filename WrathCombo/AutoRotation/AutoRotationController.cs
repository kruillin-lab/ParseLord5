#region

using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using WrathCombo.API.Enum;
using WrathCombo.Combos.PvE;
using WrathCombo.Combos.PvE.Enums;
using WrathCombo.Core;
using WrathCombo.CustomComboNS;
using WrathCombo.CustomComboNS.Functions;
using WrathCombo.Data;
using WrathCombo.Extensions;
using WrathCombo.Native;
using WrathCombo.Services;
using WrathCombo.Services.IPC_Subscriber;
using WrathCombo.Window.Functions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;
using static WrathCombo.CustomComboNS.Functions.Jobs;
using static WrathCombo.Data.ActionWatching;
using ActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

#endregion

namespace WrathCombo.AutoRotation;

internal unsafe class AutoRotationController
{
    public static AutoRotationConfigIPCWrapper? cfg;

    public static long HealThrottle = 0;

    static bool _lockedST = false;
    static bool _lockedAoE = false;

    static DateTime? TimeToHeal;

    const float QueryRange = 30f;

    public static bool WouldLikeToGroundTarget;
    public static bool Paused;
    public static int UnpauseSeconds = 0;
    public static bool IsIssuingAutorotAction;
    public static bool IsSelectingAutorotAction;
    public static bool IsIssuingManualQueuedAction;
    private static long ManualOverrideUntil;
    private static uint ManualQueuedActionId;
    private static ulong ManualQueuedTargetId;
    private static long ManualQueuedUntil;
    private const long ManualOverrideDurationMs = 900;
    private const long ManualQueueGraceMs = 1500;
    private static readonly uint[] ManualOverrideActionBlacklist =
    [
        DNC.StandardStep,
        DNC.TechnicalStep,
        DNC.StandardFinish0,
        DNC.StandardFinish1,
        DNC.StandardFinish2,
        DNC.TechnicalFinish0,
        DNC.TechnicalFinish1,
        DNC.TechnicalFinish2,
        DNC.TechnicalFinish3,
        DNC.TechnicalFinish4,
        DNC.Emboite,
        DNC.Entrechat,
        DNC.Jete,
        DNC.Pirouette,
        DNC.FinishingMove,
        SGE.Eukrasia,
        SGE.EukrasianDosis,
        SGE.EukrasianDosis2,
        SGE.EukrasianDosis3,
        SGE.EukrasianDyskrasia,
        SGE.EukrasianDiagnosis,
        SGE.EukrasianPrognosis,
        SGE.EukrasianPrognosis2,
        NIN.Ninjutsu,
        NIN.Rabbit,
    ];

    public static IGameObject? AutorotHealTarget;
    public static bool AutorotRaidwiding;
    public static int AutorotRaidwides = 0;
    public static bool TankbusterHandled = false;

    public AutoRotationController()
    {
        OnPartyCombatChanged += ResetError;
        Svc.Chat.ChatMessage += ScanForWarnings;
        OnStatusChanged += StatusChanged;
    }

    private static void TraceWhmHeal(string key, string message, TimeSpan? throttle = null)
    {
        if (Player.Job is not Job.WHM)
            return;

        if (!EzThrottler.Throttle($"PL5-WHM-HEAL-{key}", throttle ?? TimeSpan.FromSeconds(1)))
            return;

        Svc.Log.Information($"[PL5-WHM-HEAL] {message}");
    }

    private static string DescribeWhmTarget(IGameObject? target)
    {
        if (target is null)
            return "null";

        try
        {
            return $"{target.Name}/{target.GameObjectId:X} hp={GetTargetHPPercent(target, cfg?.HealerSettings.IncludeShields ?? false):0.0}";
        }
        catch
        {
            return $"{target.Name}/{target.GameObjectId:X} hp=?";
        }
    }

    private static string DescribeWhmAction(uint action) =>
        $"{action.ActionName()}({action})";

    private static (int party, int outOfPartyNpc, int aoeLowAll, int aoeEligible) GetWhmHealSnapshot()
    {
        try
        {
            var members = GetPartyMembers()
                .Where(x => x.BattleChara is not null && !x.BattleChara.IsDead && x.BattleChara.IsTargetable)
                .ToList();

            var aoeLowAll = members.Count(x =>
                GetTargetHPPercent(x.BattleChara, cfg.HealerSettings.IncludeShields) <= cfg.HealerSettings.AoETargetHPP);

            var aoeEligible = members.Count(x =>
                !x.IsOutOfPartyNPC &&
                !x.BattleChara!.StatusList.Any(status => StatusCache.DoNotHealStatuses.Contains(status.StatusId)) &&
                GetTargetDistance(x.BattleChara) <= 20f &&
                GetTargetHPPercent(x.BattleChara, cfg.HealerSettings.IncludeShields) <= cfg.HealerSettings.AoETargetHPP);

            return (members.Count, members.Count(x => x.IsOutOfPartyNPC), aoeLowAll, aoeEligible);
        }
        catch
        {
            return (-1, -1, -1, -1);
        }
    }

    private void StatusChanged(uint statusId, bool onPlayer)
    {
        Svc.Log.Verbose($"[AutoRotStatusCheck] {((ushort)statusId).StatusName()} {(onPlayer ? "Gained" : "Lost")}");
    }

    private void ScanForWarnings(Dalamud.Game.Chat.IHandleableChatMessage message)
    {
        if (message.LogKind != Dalamud.Game.Text.XivChatType.SystemMessage)
            return;

        bool pauseWarningFound = false;
        var logMessages = Svc.Data.Excel.GetSheet<LogMessage>();
        switch (Content.TerritoryID)
        {
            default:
                break;
        }

        if (pauseWarningFound)
        {
            Paused = true;
            Svc.Framework.RunOnTick(() => Paused = false, TimeSpan.FromSeconds(UnpauseSeconds));
        }
    }

    public void Dispose()
    {
        OnPartyCombatChanged -= ResetError;
        Svc.Chat.ChatMessage -= ScanForWarnings;
        // OnStatusChanged is a static event, so a missed unsubscribe keeps this
        // instance -- and the assembly -- alive after the plugin unloads.
        OnStatusChanged -= StatusChanged;
        // Static throttle state: without a reset the next enable inherits a
        // future deadline and StallWatch stays silent for up to 8s.
        _nextStallWarnAt = 0;
        // The rest of the mutable statics on this class survive a reused load
        // context the same way. AutorotHealTarget is the dangerous one: it
        // holds a game object from the previous session, which the next enable
        // would read before the first target scan replaces it.
        AutorotHealTarget = null;
        AutorotRaidwiding = false;
        AutorotRaidwides = 0;
        TankbusterHandled = false;
        WouldLikeToGroundTarget = false;
        IsIssuingAutorotAction = false;
        IsSelectingAutorotAction = false;
        IsIssuingManualQueuedAction = false;
        Paused = false;
        UnpauseSeconds = 0;
        HealThrottle = 0;
        TimeToHeal = null;
        _lockedST = false;
        _lockedAoE = false;
    }

    private void ResetError(bool state)
    {
        if (!state)
            Paused = false;
    }

    static Func<WrathPartyMember, bool> RezQuery => x =>
        x.BattleChara is not null &&
        x.BattleChara.IsDead &&
        x.BattleChara.IsTargetable &&
        (cfg.HealerSettings.AutoRezOutOfParty || GetPartyMembers().Any(y => y.GameObjectId == x.BattleChara.GameObjectId)) &&
        GetTargetDistance(x.BattleChara) <= QueryRange &&
        !HasStatusEffect(2648, x.BattleChara, true) && // Transcendent Effect
        !HasStatusEffect(148, x.BattleChara, true) && // Raise Effect
        !HasStatusEffect(4263, x.BattleChara, true) && // Raise Denied (OC)
        TimeSpentDead(x.BattleChara.GameObjectId).TotalSeconds > 2;

    public static bool LockedST
    {
        get => _lockedST;
        set
        {
            //if (_lockedST != value)
            //    Svc.Log.Debug($"Locked ST updated to {value}");

            _lockedST = value;
        }
    }
    public static bool LockedAoE
    {
        get => _lockedAoE;
        set
        {
            //if (_lockedAoE != value)
            //    Svc.Log.Debug($"Locked AoE updated to {value}");

            _lockedAoE = value;
        }
    }

    static bool CombatBypass => DPSTargeting.BaseSelection.Any(x => (cfg.BypassQuest && IsQuestMob(x)) || (cfg.BypassFATE && x.Struct()->FateId != 0 && InFATE()));
    static bool NotInCombat => !GetPartyMembers().Any(x => x.BattleChara is not null && x.BattleChara.Struct()->InCombat && !x.IsOutOfPartyNPC) || PartyEngageDuration().TotalSeconds < cfg.CombatDelay;

    private static bool ShouldSkipAutorotation()
    {
        return !cfg.Enabled
               || !Player.Available
               || Player.Object.IsDead
               || IsOccupied()
               || Player.Mounted
               || !EzThrottler.Throttle("Autorot", cfg.Throttler)
               || (cfg.DPSSettings.UnTargetAndDisableForPenalty && PlayerHasActionPenalty())
               || (ActionManager.Instance()->QueuedActionId > 0 && !Service.Configuration.OverwriteQueue && !HasManualQueuedAction())
               || (Environment.TickCount64 < ManualOverrideUntil && !HasManualQueuedAction())
               || Paused;
    }

    private const int StallWarnSeconds = 8;
    private static long _nextStallWarnAt;

    /// <summary>
    ///     ParseLord5 diagnostic: if auto-rotation is enabled, we are in combat with a
    ///     battle target, and nothing has been sent for StallWarnSeconds, say so once
    ///     per window - turns silent rotation stalls into log evidence.
    /// </summary>
    private static void MaybeWarnRotationStalled()
    {
        if (cfg?.Enabled != true || !InCombat() || !HasBattleTarget())
            return;

        var idle = (DateTime.Now - ActionWatching.TimeLastActionUsed).TotalSeconds;
        if (idle < StallWarnSeconds)
            return;

        var now = Environment.TickCount64;
        if (now < _nextStallWarnAt)
            return;
        _nextStallWarnAt = now + StallWarnSeconds * 1000;

        Svc.Log.Information(
            $"[ParseLord5][StallWatch] No action sent for {idle:F0}s with autorot on " +
            $"(job={Player.Job}, weaveCount={ActionWatching.WeaveActions.Count})");
    }

    private static bool HasManualQueuedAction()
    {
        if (ManualQueuedActionId == 0)
            return false;

        if (Environment.TickCount64 <= ManualQueuedUntil)
            return true;

        ClearManualQueuedAction();
        return false;
    }

    private static void ClearManualQueuedAction()
    {
        ManualQueuedActionId = 0;
        ManualQueuedTargetId = 0;
        ManualQueuedUntil = 0;
        // Release the autorot suppression window with the press it was protecting;
        // the successful-replay path re-arms it immediately after this call.
        ManualOverrideUntil = 0;
    }

    private static void LogManualQueuedActionDropped(uint actionId, string reason)
    {
        if (EzThrottler.Throttle("ManualQueuedActionDropped", TimeSpan.FromSeconds(5)))
            Svc.Log.Debug($"[ManualQueuedAction] Dropped {actionId.ActionName()} ({actionId}): {reason}");
    }

    internal static void NoteManualActionOverride(uint actionId)
    {
        if (!IsManualActionOverrideCandidate(actionId))
            return;

        ManualOverrideUntil = Environment.TickCount64 + ManualOverrideDurationMs;
    }

    internal static void NoteManualQueuedGcd(uint actionId, ulong targetId)
    {
        if (!IsManualActionOverrideCandidate(actionId))
            return;

        ManualQueuedActionId = actionId;
        ManualQueuedTargetId = targetId;
        ManualQueuedUntil = Environment.TickCount64 + (long)(MathF.Max(RemainingGCD, 0.1f) * 1000) + ManualQueueGraceMs;
    }

    internal static bool IsManualActionOverrideCandidate(uint actionId) =>
        !IsIssuingAutorotAction &&
        cfg?.Enabled == true &&
        actionId != 0 &&
        actionId < All.SingleTargetDPS && // custom-action buttons are not manual presses
        !IsManualOverrideBlacklisted(actionId) &&
        RemainingGCD > 0;

    private static bool IsManualOverrideBlacklisted(uint actionId) =>
        NIN.MudraSigns.Contains(actionId) ||
        ManualOverrideActionBlacklist.Contains(actionId);

    private static bool UseAutorotAction(ActionType actionType, uint actionId)
    {
        IsIssuingAutorotAction = true;
        try
        {
            if (actionType == ActionType.Action &&
                ActionStacksEXIPC.TryPrepareAction(actionId, 0xE000_0000, out var preparedActionId, out var preparedTargetId, out var stackName))
            {
                var ret = ActionManager.Instance()->UseAction(actionType, preparedActionId, preparedTargetId);
                Svc.Log.Debug($"[ActionStacksEXIPC] Prepared '{stackName}': {actionId.ActionName()} -> {preparedActionId.ActionName()} target={preparedTargetId:X} ret={ret}");
                return ret;
            }

            return ActionManager.Instance()->UseAction(actionType, actionId);
        }
        finally
        {
            IsIssuingAutorotAction = false;
        }
    }

    private static bool UseAutorotAction(ActionType actionType, uint actionId, ulong targetId)
    {
        IsIssuingAutorotAction = true;
        try
        {
            if (actionType == ActionType.Action &&
                ActionStacksEXIPC.TryPrepareAction(actionId, targetId, out var preparedActionId, out var preparedTargetId, out var stackName))
            {
                var ret = ActionManager.Instance()->UseAction(actionType, preparedActionId, preparedTargetId);
                Svc.Log.Debug($"[ActionStacksEXIPC] Prepared '{stackName}': {actionId.ActionName()} -> {preparedActionId.ActionName()} target={preparedTargetId:X} ret={ret}");
                return ret;
            }

            return ActionManager.Instance()->UseAction(actionType, actionId, targetId);
        }
        finally
        {
            IsIssuingAutorotAction = false;
        }
    }

    private static bool TryResolveManualQueuedTarget(uint actionId, bool areaTargeted, ref ulong targetId)
    {
        if (areaTargeted)
            return true;

        var targetObject = targetId.GetObject();
        if (targetObject is not null && ActionManager.CanUseActionOnTarget(actionId, targetObject.Struct()))
            return true;

        if (Svc.Targets.SoftTarget is { } softTarget &&
            ActionManager.CanUseActionOnTarget(actionId, softTarget.Struct()))
        {
            targetId = softTarget.GameObjectId;
            return true;
        }

        if (Svc.Targets.Target is { } hardTarget &&
            ActionManager.CanUseActionOnTarget(actionId, hardTarget.Struct()))
        {
            targetId = hardTarget.GameObjectId;
            return true;
        }

        if (Player.Object is not null &&
            ActionManager.CanUseActionOnTarget(actionId, Player.GameObject))
        {
            targetId = Player.Object.GameObjectId;
            return true;
        }

        return false;
    }

    private static bool TryUseManualQueuedAction()
    {
        if (!HasManualQueuedAction())
            return false;

        var actionId = ManualQueuedActionId;
        if (ActionManager.Instance()->QueuedActionId != 0 && ActionManager.Instance()->QueuedActionId != actionId)
        {
            ActionManager.Instance()->QueuedActionId = 0;
            ActionManager.Instance()->QueuedTargetId = 0;
        }

        if (!AutoRotationHelper.AutoRotCanPressAction(actionId))
            return true;

        var targetId = ManualQueuedTargetId;
        var areaTargeted = ActionSheet[actionId].TargetArea;
        if (!TryResolveManualQueuedTarget(actionId, areaTargeted, ref targetId))
        {
            LogManualQueuedActionDropped(actionId, "original target disappeared and no valid fallback target was available");
            ClearManualQueuedAction();
            return false;
        }

        WouldLikeToGroundTarget = areaTargeted;
        IsIssuingManualQueuedAction = true;
        bool ret;
        try
        {
            ret = UseAutorotAction(ActionType.Action, actionId, targetId);
        }
        finally
        {
            IsIssuingManualQueuedAction = false;
        }
        WouldLikeToGroundTarget = false;

        if (ret)
        {
            ClearManualQueuedAction();
            ManualOverrideUntil = Environment.TickCount64 + ManualOverrideDurationMs;
        }
        else if (Environment.TickCount64 >= ManualQueuedUntil)
        {
            LogManualQueuedActionDropped(actionId, "manual queue grace expired after replay failed");
            ClearManualQueuedAction();
        }

        return true;
    }

    internal static void Run()
    {
        cfg ??= new AutoRotationConfigIPCWrapper(Service.Configuration.RotationConfig);

        if (!cfg.Enabled)
            OverrideTarget = null;

        // Early exit for all conditions that should prevent autorotation
        if (ShouldSkipAutorotation())
            return;

        MaybeWarnRotationStalled();

        uint _ = 0;
        var autoActions = Presets.GetJobAutorots;

        if (TryUseManualQueuedAction())
            return;

        // Pre-emptive HoT/Shield for healers
        if (cfg.HealerSettings.PreEmptiveHoT && Player.Job is Job.CNJ or Job.WHM or Job.AST)
            PreEmptiveHot();

        if (cfg.HealerSettings.PreEmptiveHoT && Player.Job is Job.SGE or Job.SCH)
            PreEmptiveShield();

        // Bypass buffs logic
        if (cfg.BypassBuffs && NotInCombat)
        {
            if (ProcessAutoActions(autoActions, ref _, false, true))
                return;
        }

        // Only run in combat if required
        if (cfg.InCombatOnly && NotInCombat && !CombatBypass)
            return;

        // Check for Pyretic / Reasons to stop
        if (cfg.DPSSettings.UnTargetAndDisableForPenalty && PlayerHasActionPenalty())
            return;

        // Healer logic
        bool isHealer = Player.Object?.Role is CombatRole.Healer;

        if (cfg.HealerSettings.HandleRaidwides)
        {
            if (ShouldHandleHealerRaidwides(isHealer) && GroupDamageIncoming(out var multi))
            {
                AutorotRaidwiding = true;
                HandleRaidwide(multi);
            }
            else
            {
                if (AutorotRaidwides > 0)
                {
                    Svc.Log.Debug($"Used {AutorotRaidwides} raidwides {string.Join(", ", BlacklistedRaidwides.Select(x => x.ActionName()))}");
                }
                BlacklistedRaidwides.Clear();
                AutorotRaidwides = 0;
                AutorotRaidwiding = false;
            }
        }

        if (cfg.HealerSettings.HandleTankbusters)
        {
            if (isHealer && TryGetTankBusterTarget(out var tbtarget))
            {
                HandleTankbuster(tbtarget.SafeGameObjectId);
            }
            else
                TankbusterHandled = false;
        }

        var healTarget = isHealer ? AutoRotationHelper.GetSingleTarget(cfg.HealerRotationMode) : null;

        bool aoeheal = isHealer
                       && HealerTargeting.CanAoEHeal()
                       && autoActions.Any(x => x.Key.Attributes().AutoAction?.IsHeal == true && x.Key.Attributes().AutoAction?.IsAoE == true);

        bool needsHeal = ((healTarget != null
                           && autoActions.Any(x => x.Key.Attributes().AutoAction?.IsHeal == true && x.Key.Attributes().AutoAction?.IsAoE != true))
                          || aoeheal)
                         && isHealer;

        if (needsHeal && TimeToHeal is null)
            TimeToHeal = DateTime.Now;
        else if (!needsHeal)
            TimeToHeal = null;

        // Check if any healing action is ready
        bool actCheck = autoActions.Any(x =>
        {
            var attr = x.Key.Attributes();
            uint gameAct = 0;
            var outAct = AutoRotationHelper.InvokeCombo(x.Key, attr, ref gameAct, selectingAutorotAction: true);
            bool peeked = ActionStacksEXIPC.TryPeekAction(
                AutorotActionPolicy.PeekKey(gameAct, outAct, Service.Configuration.ActionChanging),
                Player.Object?.GameObjectId ?? 0xE000_0000,
                out var asResolvedAction,
                out var _,
                out var _);
            uint actionToCheck = AutorotActionPolicy.ResolveAction(outAct, peeked, asResolvedAction);
            return attr.AutoAction?.IsHeal == true && ActionReady(actionToCheck);
        });

        // ParseLord5: Dynamic linear reaction delay scaled by lowest HP% among heal targets
        float lowestHp = 100f;
        if (healTarget != null)
        {
            lowestHp = GetTargetHPPercent(healTarget);
        }
        else if (aoeheal)
        {
            foreach (var member in GetPartyMembers())
            {
                if (member.BattleChara is not null && !member.BattleChara.IsDead)
                {
                    float hp = GetTargetHPPercent(member.BattleChara);
                    if (hp < lowestHp)
                        lowestHp = hp;
                }
            }
        }

        double effectiveHealDelay = HealDelayCurve.ComputeEffectiveHealDelay(
            cfg.HealerSettings.HealDelay, lowestHp, true);

        bool canHeal = TimeToHeal is not null
                       && (DateTime.Now - TimeToHeal.Value).TotalSeconds >= effectiveHealDelay
                       && actCheck;

        var whmSnapshot = GetWhmHealSnapshot();
        var healAge = TimeToHeal is null ? "none" : $"{(DateTime.Now - TimeToHeal.Value).TotalSeconds:0.0}s";
        TraceWhmHeal(
            "decision",
            $"phase=decision target={DescribeWhmTarget(healTarget)} lowestHp={lowestHp:0.0} " +
            $"party={whmSnapshot.party} oopNpc={whmSnapshot.outOfPartyNpc} aoeLowAll={whmSnapshot.aoeLowAll} " +
            $"aoeEligible={whmSnapshot.aoeEligible}/{cfg.HealerSettings.AoEHealTargetCount} " +
            $"needsHeal={needsHeal} aoeheal={aoeheal} actCheck={actCheck} canHeal={canHeal} " +
            $"healAge={healAge} delay={effectiveHealDelay:0.0}s rotationEnabled={cfg.Enabled}");

        // Healer cleanse/rez logic
        if (isHealer ||
            (Player.Job is Job.SMN or Job.RDM && cfg.HealerSettings.AutoRezDPSJobs) ||
            OccultCrescent.IsEnabledAndUsable(Preset.Phantom_Chemist_Revive, OccultCrescent.Revive) ||
            Variant.CanRaise())
        {
            if (ActionManager.Instance()->QueuedActionId == RoleActions.Healer.Esuna)
                ActionManager.Instance()->QueuedActionId = 0;

            if ((!needsHeal || GetPartyMembers().Any(x => HasCleansableDoom(x.BattleChara))) && WrathOpener.CurrentOpener?.CurrentState is not
                OpenerState.InOpener)
            {
                if (cfg.HealerSettings.AutoCleanse && isHealer)
                    CleanseParty();

                if (cfg.HealerSettings.AutoRez)
                    RezParty();
            }
        }

        // SGE Kardia logic
        if (Player.Job is Job.SGE && cfg.HealerSettings.ManageKardia)
            UpdateKardiaTarget();

        // Reset locks if no action for 3 seconds
        if (TimeSinceLastAction.TotalSeconds >= 3)
        {
            LockedAoE = false;
            LockedST = false;
        }

        ProcessAutoActions(autoActions, ref _, canHeal, false);
    }

    private static bool ShouldHandleHealerRaidwides(bool isHealer)
    {
        if (!isHealer || !InCombat())
            return false;

        if (InBossEncounter())
            return true;

        return InDuty() && IsInParty(2);
    }

    public static IEnumerable<uint> TankbusterActions =
    [
        WHM.Aquaveil,
        WHM.DivineBenison,
        SCH.Protraction,
        SCH.Adloquium,
        SCH.Manifestation,
        AST.Spire,
        AST.Bole,
        AST.CelestialIntersection,
        AST.Exaltation,
        SGE.Taurochole,
        SGE.Eukrasia,
        SGE.EukrasianDiagnosis,
    ];

    private static void HandleTankbuster(ulong? safeGameObjectId)
    {
        if (safeGameObjectId == null)
            return;

        foreach (var spell in TankbusterActions)
        {
            if (TankbusterHandled)
                return;

            if (AbleToCast(spell, safeGameObjectId))
            {
                var act = spell;
                if (act == AST.Bole) act = AST.Play2;
                if (act == AST.Spire) act = AST.Play3;
                WouldLikeToGroundTarget = ActionSheet[act].TargetArea;
                UseAutorotAction(ActionType.Action, act is SGE.Eukrasia ? act.Retarget(SimpleTarget.Self) : act.Retarget(safeGameObjectId.GetObject()), safeGameObjectId!.Value);
                WouldLikeToGroundTarget = false;
                if (act != SGE.Eukrasia)
                    TankbusterHandled = true;
                return;
            }
        }
    }

    private static bool AbleToCast(uint spell, ulong? safeGameObjectId = null)
    {
        return ActionReady(spell) && (safeGameObjectId != null ? !JustUsedOn(spell, safeGameObjectId.GetObject(), 5) : !JustUsed(spell, 10)) && LocalPlayer.CastActionId != spell && (!IsMoving(true) || ActionManager.GetAdjustedCastTime(ActionType.Action, spell) == 0);
    }

    public static IEnumerable<(uint Action, bool MultiHitOnly)> RaidwideActions =
    [
        (WHM.LiturgyOfTheBell.Retarget(SimpleTarget.Self), true),
        (WHM.PlenaryIndulgence, false),
        (WHM.Temperance, false),
        (WHM.DivineCaress, false),
        (WHM.Asylum.Retarget(SimpleTarget.Self), false),
        (WHM.Medica2, false),
        (WHM.Medica3, false),
        (SCH.Expedient, false),
        (SCH.Seraphism, false),
        (SCH.Succor, false),
        (SCH.Accession, false),
        (SCH.Concitation, false),
        (AST.CollectiveUnconscious, false),
        (AST.SunSign, false),
        (AST.CelestialOpposition, false),
        (AST.AspectedHelios, false),
        (AST.HeliosConjuction, false),
        (SGE.Panhaima, true),
        (SGE.Kerachole, false),
        (SGE.Physis, false),
        (SGE.Physis2, false),
        (SGE.Holos, false),
        (SGE.Eukrasia, false),
        (SGE.EukrasianPrognosis, false),
        (SGE.EukrasianPrognosis2, false),
    ];

    public static List<uint> BlacklistedRaidwides = [];

    private static bool UsedRaidwideAbilityThisGcd()
    {
        foreach (var action in WeaveActions)
        {
            if (action.ActionAttackType() is not ActionAttackType.Ability)
                continue;

            foreach (var (raidwide, _) in RaidwideActions)
            {
                if (raidwide == action)
                    return true;
            }
        }

        return false;
    }

    private static void HandleRaidwide(bool multihit)
    {
        foreach (var (spell, multihitter) in RaidwideActions)
        {
            if (spell.ActionAttackType() is ActionAttackType.Ability && UsedRaidwideAbilityThisGcd())
                return;

            int numberOfCasts = GetPartyAvgHPPercent() switch
            {
                <= 30 => 3,
                <= 60 => 2,
                _ => 1
            };

            if (AutorotRaidwides >= numberOfCasts)
                return;

            if (!multihit && multihitter)
                continue;

            if (BlacklistedRaidwides.Contains(spell))
                continue;

            if (AbleToCast(spell))
            {
                WouldLikeToGroundTarget = ActionSheet[spell].TargetArea;
                UseAutorotAction(ActionType.Action, spell);
                WouldLikeToGroundTarget = false;
                return;
            }
        }
    }

    private static bool ProcessAutoActions(Dictionary<Preset, bool> autoActions, ref uint _, bool canHeal, bool stOnly)
    {
        // Pre-filter and cache attributes to avoid repeated lookups
        var filteredActions = autoActions
            .Select(x => new { Preset = x.Key, Attributes = x.Key.Attributes() })
            .Where(x => x.Attributes is { AutoAction: not null, ReplaceSkill: not null })
            .Where(x => x.Attributes.AutoAction.IsHeal == canHeal)
            .Where(x => !stOnly || x.Attributes.AutoAction.IsAoE == false)
            .OrderByDescending(x => x.Attributes.AutoAction.IsAoE);

        foreach (var entry in filteredActions)
        {
            var attributes = entry.Attributes;
            var action = attributes.AutoAction!;

            // Skip if locked
            if ((action.IsAoE && LockedST) || (!action.IsAoE && LockedAoE))
                continue;

            // Skip if rez invuln is up
            if (!action.IsHeal && HasStatusEffect(418))
                continue;

            uint gameAct = attributes.ReplaceSkill!.ActionIDs.First();
            var status = ActionManager.Instance()->GetActionStatus(ActionType.Action, gameAct, checkCastingActive: false, checkRecastActive: false);

            if (!LevelChecked(gameAct) || status == 581)
                continue;

            if (action.IsHeal)
            {
                AutomateHealing(entry.Preset, attributes, gameAct);
                continue;
            }

            // Tank logic
            if (Player.Object?.GetRole() is CombatRole.Tank)
            {
                AutomateTanking(entry.Preset, attributes, gameAct);
                continue;
            }

            // DPS logic
            if (!action.IsHeal && AutomateDPS(entry.Preset, attributes, gameAct))
                return false;
        }

        return false;
    }

    private static void PreEmptiveHot()
    {
        if (PartyInCombat() || SimpleTarget.FocusTarget is null || (InDuty() && !Svc.DutyState.IsDutyStarted))
            return;

        ushort regenBuff = Player.Job switch
        {
            Job.AST => AST.Buffs.AspectedBenefic,
            Job.CNJ or Job.WHM => WHM.Buffs.Regen,
            _ => 0
        };

        uint regenSpell = Player.Job switch
        {
            Job.AST => AST.AspectedBenefic,
            Job.CNJ or Job.WHM => WHM.Regen,
            _ => 0
        };

        if (regenSpell != 0 && !JustUsed(regenSpell, 4) && SimpleTarget.FocusTarget != null && (!HasStatusEffect(regenBuff, out var regen, SimpleTarget.FocusTarget) || regen?.RemainingTime <= 5f))
        {
            var query = Svc.Objects.Where(x => !x.IsDead && x.IsTargetable && x.IsHostile());
            if (!query.Any())
                return;

            if (query.Min(x => GetTargetDistance(x, SimpleTarget.FocusTarget)) <= QueryRange)
            {
                var spell = ActionManager.Instance()->GetAdjustedActionId(regenSpell).Retarget(SimpleTarget.FocusTarget);

                if (SimpleTarget.FocusTarget.IsDead)
                    return;

                if (!ActionReady(spell))
                    return;

                if (Player.Object is not null && ActionManager.CanUseActionOnTarget(spell, SimpleTarget.FocusTarget.Struct()) && !OutOfRange(spell, Player.Object, SimpleTarget.FocusTarget) && ActionManager.Instance()->GetActionStatus(ActionType.Action, spell) == 0)
                {
                    UseAutorotAction(ActionType.Action, regenSpell, SimpleTarget.FocusTarget.GameObjectId);
                    return;
                }
            }
        }
    }

    private static void PreEmptiveShield()
    {
        if (InCombat() || PartyInCombat() || SimpleTarget.FocusTarget is null || (InDuty() && !Svc.DutyState.IsDutyStarted))
            return;

        ushort shieldBuff = Player.Job switch
        {
            Job.SGE => SGE.Buffs.EukrasianDiagnosis,
            Job.SCH => SCH.Buffs.Galvanize,
            _ => 0
        };

        uint shieldSpell = Player.Job switch
        {
            Job.SGE => SGE.EukrasianDiagnosis,
            Job.SCH => SCH.Adloquium,
            _ => 0
        };

        uint prepSpell = Player.Job switch
        {
            Job.SGE => SGE.Eukrasia,
            _ => 0
        };

        if (shieldSpell != 0 && !JustUsed(shieldSpell, 4) && SimpleTarget.FocusTarget != null && (!HasStatusEffect(shieldBuff, out var shield, SimpleTarget.FocusTarget) || shield?.RemainingTime <= 1f))
        {
            if (prepSpell != 0 && !JustUsed(prepSpell, 4) && !HasStatusEffect(SGE.Buffs.Eukrasia))
            {
                var spell = ActionManager.Instance()->GetAdjustedActionId(prepSpell).Retarget(SimpleTarget.FocusTarget);

                if (!ActionReady(prepSpell))
                    return;

                if (ActionManager.Instance()->GetActionStatus(ActionType.Action, spell) == 0)
                {
                    UseAutorotAction(ActionType.Action, prepSpell);
                    return;
                }
            }

            var query = Svc.Objects.Where(x => !x.IsDead && x.IsTargetable && x.IsHostile());
            if (!query.Any())
                return;

            if (query.Min(x => GetTargetDistance(x, SimpleTarget.FocusTarget)) <= QueryRange)
            {
                var spell = ActionManager.Instance()->GetAdjustedActionId(shieldSpell).Retarget(SimpleTarget.FocusTarget);

                if (SimpleTarget.FocusTarget.IsDead)
                    return;

                if (!ActionReady(spell) ||
                    ActionManager.GetAdjustedCastTime(ActionType.Action, spell) > 0 && TimeStoodStill < TimeSpan.FromSeconds(1))
                    return;

                if (Player.Object is not null && ActionManager.CanUseActionOnTarget(spell, SimpleTarget.FocusTarget.Struct()) && !OutOfRange(spell, Player.Object, SimpleTarget.FocusTarget) && ActionManager.Instance()->GetActionStatus(ActionType.Action, spell) == 0)
                {
                    UseAutorotAction(ActionType.Action, spell, SimpleTarget.FocusTarget.GameObjectId);
                    return;
                }
            }
        }
    }

    // Note: Similar to Kardia, because this has its own set of rules but regarding timings I'm not sure if I want to wire this up to retargeting
    private static void RezParty()
    {
        if (HasStatusEffect(418)) return;
        uint resSpell = 0;

        if (OccultCrescent.IsEnabledAndUsable(Preset.Phantom_Chemist_Revive, OccultCrescent.Revive))
        {
            resSpell = OccultCrescent.Revive;
        }
        else if (Variant.CanRaise())
        {
            resSpell = Variant.Raise;
        }
        else
        {
            resSpell = Player.Job switch
            {
                Job.CNJ or Job.WHM => WHM.Raise,
                Job.SCH or Job.SMN => SCH.Resurrection,
                Job.AST => AST.Ascend,
                Job.SGE => SGE.Egeiro,
                Job.RDM => RDM.Verraise,
                _ => 0,
            };
        }

        if (resSpell == 0)
            return;

        IEnumerable<WrathPartyMember> deadPeople = DeadPeople;

        if (cfg.HealerSettings.AutoRezDPSJobsHealersOnly && Player.Job is Job.RDM or Job.SMN)
        {
            deadPeople = deadPeople.Where(x => x.GetRole() is CombatRole.Healer || x.RealJob?.GetJob() is Job.SMN or Job.RDM);
        }

        if (ActionManager.Instance()->QueuedActionId == resSpell)
            ActionManager.Instance()->QueuedActionId = 0;

        if (Player.Object.CurrentMp >= GetResourceCost(resSpell) && ActionReady(resSpell))
        {
            var timeSinceLastRez = TimeSinceLastSuccessfulCast(resSpell);
            if ((timeSinceLastRez != -1f && timeSinceLastRez < 4f) || Player.Object.IsCasting())
                return;

            if (deadPeople.Where(RezQuery).FindFirst(x => x is not null, out var member))
            {
                if (resSpell == OccultCrescent.Revive)
                {
                    UseAutorotAction(ActionType.Action, resSpell, member.BattleChara.GameObjectId);
                    return;
                }

                if (resSpell is Variant.Raise)
                {
                    //Try to Swiftcast if Magic DPS
                    if (GetRoleFromJob(Player.Job) is JobRole.MagicalDPS)
                    {
                        if (ActionReady(RoleActions.Magic.Swiftcast) && !HasStatusEffect(RDM.Buffs.Dualcast))
                        {
                            if (ActionManager.Instance()->GetActionStatus(ActionType.Action, RoleActions.Magic.Swiftcast) == 0)
                            {
                                UseAutorotAction(ActionType.Action, RoleActions.Magic.Swiftcast);
                                return;
                            }
                        }
                    }

                    if (HasStatusEffect(RoleActions.Magic.Buffs.Swiftcast) || HasStatusEffect(RDM.Buffs.Dualcast) || !IsMoving())
                    {
                        UseAutorotAction(ActionType.Action, resSpell, member.BattleChara.GameObjectId);
                        return;
                    }
                }

                if (Player.Job is Job.RDM)
                {
                    if (ActionReady(RoleActions.Magic.Swiftcast) && !HasStatusEffect(RDM.Buffs.Dualcast))
                    {
                        UseAutorotAction(ActionType.Action, RoleActions.Magic.Swiftcast);
                        return;
                    }

                    if (ActionManager.GetAdjustedCastTime(ActionType.Action, resSpell) == 0)
                    {
                        UseAutorotAction(ActionType.Action, resSpell, member.BattleChara.GameObjectId);
                    }

                }
                else
                {
                    if (ActionReady(RoleActions.Magic.Swiftcast))
                    {
                        if (ActionManager.Instance()->GetActionStatus(ActionType.Action, RoleActions.Magic.Swiftcast) == 0)
                        {
                            UseAutorotAction(ActionType.Action, RoleActions.Magic.Swiftcast);
                            return;
                        }
                    }

                    if (!IsMoving() || HasStatusEffect(RoleActions.Magic.Buffs.Swiftcast))
                    {

                        if ((cfg is not null) && ((cfg.HealerSettings.AutoRezRequireSwift && ActionManager.GetAdjustedCastTime(ActionType.Action, resSpell) == 0) || !cfg.HealerSettings.AutoRezRequireSwift))
                        {
                            UseAutorotAction(ActionType.Action, resSpell, member.BattleChara.GameObjectId);
                        }
                    }
                }
            }
        }
    }

    private static void CleanseParty()
    {
        if (HasStatusEffect(418) || LocalPlayer is not { } || !EzThrottler.Throttle("CleanseThrottle", 50)) return;

        if (SimpleTarget.Stack.AllyToEsuna is IBattleChara memberBC)
        {
            var res = ActionManager.GetActionInRangeOrLoS(Healer.Role.Esuna, LocalPlayer.GameObject(), memberBC.GameObject());
            if (res is 0 or 565)
            {
                Svc.Log.Debug($"Cleansing {memberBC.Name}");
                UseAutorotAction(ActionType.Action, RoleActions.Healer.Esuna.Retarget(memberBC), memberBC.GameObjectId);
            }
        }
    }

    // Note: Not entirely sure what to do when the Kardia standalone retargeting is on since it doesn't follow this ruleset so this will be untouched for now but
    // it is known if it acts funny with the standalone retarget then that's what causes it.
    private static void UpdateKardiaTarget()
    {
        if (HasStatusEffect(418)) return;
        if (!LevelChecked(SGE.Kardia)) return;
        if (CombatEngageDuration().TotalSeconds < 3) return;

        foreach (var member in GetPartyMembers().Where(x => !x.BattleChara.IsDead).OrderByDescending(x => x.BattleChara?.GetRole() is CombatRole.Tank))
        {
            if (cfg.HealerSettings.KardiaTanksOnly && member.BattleChara?.GetRole() is not CombatRole.Tank &&
                !HasStatusEffect(3615, member.BattleChara, true)) continue;

            var enemiesTargeting = Svc.Objects.Count(x => x.IsTargetable && x.IsHostile() && x.TargetObjectId == member.BattleChara.GameObjectId);
            if (enemiesTargeting > 0 && !HasStatusEffect(SGE.Buffs.Kardion, member.BattleChara))
            {
                UseAutorotAction(ActionType.Action, SGE.Kardia.Retarget(member.BattleChara), member.BattleChara.GameObjectId);
                return;
            }
        }

    }

    private static bool AutomateDPS(Preset preset, PresetStorage.PresetData attributes, uint gameAct)
    {
        var mode = cfg.DPSRotationMode;
        if (attributes.AutoAction!.IsAoE)
        {
            return AutoRotationHelper.ExecuteAoE(mode, preset, attributes, gameAct);
        }
        else
        {
            return AutoRotationHelper.ExecuteST(mode, preset, attributes, gameAct);
        }
    }

    private static bool AutomateTanking(Preset preset, PresetStorage.PresetData attributes, uint gameAct)
    {
        var mode = cfg.DPSRotationMode;
        if (attributes.AutoAction!.IsAoE)
        {
            return AutoRotationHelper.ExecuteAoE(mode, preset, attributes, gameAct);
        }
        else
        {
            return AutoRotationHelper.ExecuteST(mode, preset, attributes, gameAct);
        }
    }

    private static bool AutomateHealing(Preset preset, PresetStorage.PresetData attributes, uint gameAct)
    {
        var mode = cfg.HealerRotationMode;
        if (Player.Object?.IsCasting() is true)
        {
            TraceWhmHeal("automate-healing-block", $"phase=automate-healing preset={preset} block=casting");
            return false;
        }
        if (Environment.TickCount64 < HealThrottle)
        {
            TraceWhmHeal("automate-healing-block", $"phase=automate-healing preset={preset} block=heal-throttle remainingMs={HealThrottle - Environment.TickCount64}");
            return false;
        }

        if (attributes.AutoAction!.IsAoE)
        {
            var ret = AutoRotationHelper.ExecuteAoE(mode, preset, attributes, gameAct);
            TraceWhmHeal("automate-healing", $"phase=automate-healing preset={preset} lane=aoe result={ret}");
            return ret;
        }
        else
        {
            var ret = AutoRotationHelper.ExecuteST(mode, preset, attributes, gameAct);
            TraceWhmHeal("automate-healing", $"phase=automate-healing preset={preset} lane=st result={ret}");
            return ret;
        }
    }

    public static class AutoRotationHelper
    {
        public static IGameObject? GetSingleTarget(Enum rotationMode)
        {
            if (rotationMode is DPSRotationMode dpsmode)
            {
                if (Player.Object?.Role is CombatRole.Tank)
                {
                    IGameObject? target = dpsmode switch
                    {
                        DPSRotationMode.Manual =>
                            ((Svc.Targets.Target == null || !Svc.Targets.Target.IsHostile()) &&
                             cfg.DPSSettings.DPSManualFallbackMode != DPSRotationMode.Manual)
                                ? GetSingleTarget(cfg.DPSSettings.DPSManualFallbackMode)
                                : Svc.Targets.Target,
                        DPSRotationMode.Highest_Max => TankTargeting.GetHighestMaxTarget(),
                        DPSRotationMode.Lowest_Max => TankTargeting.GetLowestMaxTarget(),
                        DPSRotationMode.Highest_Current => TankTargeting.GetHighestCurrentTarget(),
                        DPSRotationMode.Lowest_Current => TankTargeting.GetLowestCurrentTarget(),
                        DPSRotationMode.Tank_Target => Svc.Targets.Target,
                        DPSRotationMode.Nearest => DPSTargeting.GetNearestTarget(),
                        DPSRotationMode.Furthest => DPSTargeting.GetFurthestTarget(),
                        _ => Svc.Targets.Target,
                    };
                    return target;
                }
                else
                {
                    IGameObject? target = dpsmode switch
                    {
                        DPSRotationMode.Manual =>
                            ((Svc.Targets.Target == null || !Svc.Targets.Target.IsHostile()) &&
                             cfg.DPSSettings.DPSManualFallbackMode != DPSRotationMode.Manual)
                                ? GetSingleTarget(cfg.DPSSettings.DPSManualFallbackMode)
                                : Svc.Targets.Target,
                        DPSRotationMode.Highest_Max => DPSTargeting.GetHighestMaxTarget(),
                        DPSRotationMode.Lowest_Max => DPSTargeting.GetLowestMaxTarget(),
                        DPSRotationMode.Highest_Current => DPSTargeting.GetHighestCurrentTarget(),
                        DPSRotationMode.Lowest_Current => DPSTargeting.GetLowestCurrentTarget(),
                        DPSRotationMode.Tank_Target => DPSTargeting.GetTankTarget(),
                        DPSRotationMode.Nearest => DPSTargeting.GetNearestTarget(),
                        DPSRotationMode.Furthest => DPSTargeting.GetFurthestTarget(),
                        _ => Svc.Targets.Target,
                    };
                    return target;
                }
            }
            if (rotationMode is HealerRotationMode healermode)
            {
                if (Player.Object?.Role != CombatRole.Healer) return null;
                IGameObject? target = healermode switch
                {
                    HealerRotationMode.Manual => HealerTargeting.ManualTarget(),
                    HealerRotationMode.Highest_Current => HealerTargeting.GetHighestCurrent(),
                    HealerRotationMode.Lowest_Current => HealerTargeting.GetLowestCurrent(),
                    _ => HealerTargeting.ManualTarget(),
                };
                AutorotHealTarget = target;
                return target;
            }

            return null;
        }

        public static bool ExecuteAoE(Enum mode, Preset preset, PresetStorage.PresetData attributes, uint gameAct)
        {
            if (LocalPlayer is not { } player)
                return false;

            var target = !cfg.DPSSettings.AoEIgnoreManual && cfg.DPSRotationMode == DPSRotationMode.Manual ?
    Svc.Targets.Target : DPSTargeting.BaseSelection.MaxBy(x => NumberOfEnemiesInRange(OriginalHook(gameAct), x, true));

            if (target is null && cfg.PauseWhenNoTarget) return true;

            if (attributes.AutoAction!.IsHeal)
            {
                LockedAoE = false;
                LockedST = false;

                uint outAct = OriginalHook(InvokeCombo(preset, attributes, ref gameAct, Player.Object));
                bool asRedirected = ActionStacksEXIPC.TryPeekAction(
                    AutorotActionPolicy.PeekKey(gameAct, outAct, Service.Configuration.ActionChanging),
                    player.GameObjectId,
                    out var asResolvedAction,
                    out var asResolvedTarget,
                    out _);
                outAct = AutorotActionPolicy.ResolveAction(outAct, asRedirected, asResolvedAction);
                var canQueue = CanQueue(outAct);
                var canAoEHeal = HealerTargeting.CanAoEHeal(outAct);
                TraceWhmHeal(
                    "execute-aoe",
                    $"phase=execute-aoe preset={preset} gameAct={DescribeWhmAction(gameAct)} outAct={DescribeWhmAction(outAct)} " +
                    $"canQueue={canQueue} canAoEHeal={canAoEHeal} target={DescribeWhmTarget(Player.Object)}");
                if (!canQueue)
                    return false;

                if (canAoEHeal)
                {
                    var castTime = ActionManager.GetAdjustedCastTime(ActionType.Action, outAct);
                    bool orbwalking = cfg.OrbwalkerIntegration && OrbwalkerIPC.CanOrbwalk;
                    if (TimeMoving.TotalMilliseconds > 0 && castTime > 0 && !orbwalking)
                        return false;

                    var targetId = player.GameObjectId;
                    var changed = CheckForChangedTarget(gameAct, ref targetId, out var replacedWith);
                    WouldLikeToGroundTarget = ActionSheet[outAct].TargetArea;
                    var ret = UseAutorotAction(ActionType.Action, Service.Configuration.ActionChanging ? gameAct : outAct, targetId);
                    WouldLikeToGroundTarget = false;

                    return true;
                }
            }
            else
            {
                if (!NIN.InMudra)
                {
                    var st = GetSingleTarget(mode);
                    var maxHit = NumberOfEnemiesInRange(DontChangeForAoe(gameAct) ? gameAct : OriginalHook(gameAct), target, true);
                    var singleTargetModeTarget = NumberOfEnemiesInRange(OriginalHook(gameAct), st, true);

                    if (singleTargetModeTarget >= maxHit)
                        target = st;

                    if (cfg.DPSSettings.DPSAoETargets == null || maxHit < cfg.DPSSettings.DPSAoETargets)
                    {
                        LockedAoE = false;
                        return false;
                    }
                    else
                    {
                        LockedAoE = true;
                        LockedST = false;
                    }
                }

                OverrideTarget = target ?? OverrideTarget;
                var issued = false;
                try
                {
                    uint outAct = OriginalHook(InvokeCombo(preset, attributes, ref gameAct, OverrideTarget));
                    bool asRedirected = ActionStacksEXIPC.TryPeekAction(
                        AutorotActionPolicy.PeekKey(gameAct, outAct, Service.Configuration.ActionChanging),
                        OverrideTarget?.GameObjectId ?? player.GameObjectId,
                        out var asResolvedAction,
                        out var asResolvedTarget,
                        out _);
                    outAct = AutorotActionPolicy.ResolveAction(outAct, asRedirected, asResolvedAction);
                    if (asRedirected)
                    {
                        if (asResolvedTarget != (OverrideTarget?.GameObjectId ?? player.GameObjectId))
                        {
                            var newTarget = asResolvedTarget.GetObject();
                            if (newTarget is not null)
                                OverrideTarget = newTarget;
                        }
                    }
                    if (!CanUseAutorotDpsAction(outAct))
                    {
                        return false;
                    }
                    if (outAct is All.SavageBlade) return true;
                    if (!CanQueue(outAct))
                    {
                        return false;
                    }

                    var sheet = ActionSheet[outAct];
                    var targetsHostile = sheet.CanTargetHostile;

                    bool switched = SwitchOnDChole(attributes, outAct, ref target);
                    var castTime = ActionManager.GetAdjustedCastTime(ActionType.Action, outAct);
                    bool orbwalking = cfg.OrbwalkerIntegration && OrbwalkerIPC.CanOrbwalk;

                    if (TimeMoving.TotalMilliseconds > 0 && castTime > 0 && !orbwalking)
                        return false;

                    if (cfg.DPSSettings.DPSAlwaysHardTarget && OverrideTarget is not null)
                        Svc.Targets.Target = OverrideTarget;

                    var canUseSelf = sheet.CanTargetSelf;
                    var areaTargeted = ActionSheet[outAct].TargetArea;
                    var acRangeCheck = ActionManager.GetActionInRangeOrLoS(outAct, player.GameObject(), OverrideTarget is null ? player.GameObject() : OverrideTarget.Struct());
                    var inRange = acRangeCheck is 0 or 565 || canUseSelf || areaTargeted;

                    if (targetsHostile && OverrideTarget is not null)
                    {
                        Svc.GameConfig.TryGet(Dalamud.Game.Config.UiControlOption.AutoFaceTargetOnAction, out uint original);
                        Svc.GameConfig.Set(Dalamud.Game.Config.UiControlOption.AutoFaceTargetOnAction, 1);
                        Vector3 pos = new(Player.Object.Position.X, Player.Object.Position.Y, Player.Object.Position.Z);
                        ActionManager.Instance()->AutoFaceTargetPosition(&pos, OverrideTarget.GameObjectId);
                        Svc.GameConfig.Set(Dalamud.Game.Config.UiControlOption.AutoFaceTargetOnAction, original);
                    }

                    if (inRange && AutoRotCanPressAction(outAct))
                    {
                        //Chance target of target.GameObjectID can be null
                        var targetId = (targetsHostile && OverrideTarget != null) || switched ? OverrideTarget.GameObjectId : canUseSelf ? player.GameObjectId : 0xE000_0000;
                        var changed = CheckForChangedTarget(gameAct, ref targetId, out var replacedWith);
                        WouldLikeToGroundTarget = areaTargeted;
                        var ret = UseAutorotAction(ActionType.Action, Service.Configuration.ActionChanging ? gameAct : outAct, targetId);
                        WouldLikeToGroundTarget = false;
                        if (NIN.MudraSigns.Contains(outAct))
                            _lockedAoE = true;
                        else
                            _lockedAoE = false;

                        issued = true;
                        return true;
                    }
                }
                finally
                {
                    if (!issued)
                        OverrideTarget = null;
                }

            }
            return false;
        }

        private static bool DontChangeForAoe(uint gameAct)
        {
            return gameAct is DNC.Windmill or DNC.Bladeshower or DNC.RisingWindmill or DNC.Bloodshower;
        }

        public static bool ExecuteST(Enum mode, Preset preset, PresetStorage.PresetData attributes, uint gameAct)
        {
            if (LocalPlayer is not { } player)
                return false;

            var target = GetSingleTarget(mode);

            if (target is null && cfg.PauseWhenNoTarget)
            {
                TraceWhmHeal("execute-st-block", $"phase=execute-st preset={preset} block=pause-no-target");
                return true;
            }

            OverrideTarget = target ?? OverrideTarget;
            var issued = false;
            try
            {
                var outAct = OriginalHook(InvokeCombo(preset, attributes, ref gameAct, target));
                bool asRedirected = ActionStacksEXIPC.TryPeekAction(
                    AutorotActionPolicy.PeekKey(gameAct, outAct, Service.Configuration.ActionChanging),
                    target?.GameObjectId ?? player.GameObjectId,
                    out var asResolvedAction,
                    out var asResolvedTarget,
                    out _);
                outAct = AutorotActionPolicy.ResolveAction(outAct, asRedirected, asResolvedAction);
                if (asRedirected)
                {
                    if (asResolvedTarget != (target?.GameObjectId ?? player.GameObjectId))
                    {
                        var newTarget = asResolvedTarget.GetObject();
                        if (newTarget is not null)
                        {
                            target = newTarget;
                            OverrideTarget = newTarget;
                        }
                    }
                }
                if (!attributes.AutoAction!.IsHeal && !CanUseAutorotDpsAction(outAct))
                {
                    return false;
                }
                TraceWhmHeal(
                    "execute-st",
                    $"phase=execute-st preset={preset} target={DescribeWhmTarget(target)} gameAct={DescribeWhmAction(gameAct)} outAct={DescribeWhmAction(outAct)} ready={ActionReady(outAct)}");
                if (!ActionReady(outAct))
                {
                    return false;
                }

                bool switched = SwitchOnDChole(attributes, outAct, ref target);
                if (outAct is DNC.ClosedPosition && DNC.DancePartnerResolver() is IBattleChara dp)
                    target = dp;

                var canUseSelf = NIN.MudraSigns.Contains(outAct)
                    ? (target is not null && target.IsHostile()) || NIN.InMudra
                    : ActionManager.CanUseActionOnTarget(outAct, Player.GameObject);

                var blockedSelfBuffs = GetCooldown(outAct).CooldownTotal >= 5;

                if (cfg.InCombatOnly && NotInCombat && !CombatBypass && !(canUseSelf && cfg.BypassBuffs && !blockedSelfBuffs))
                    return false;

                if (target is null && !canUseSelf)
                    return false;

                var areaTargeted = ActionSheet[outAct].TargetArea;
                var canUseTarget = target is not null && ActionManager.CanUseActionOnTarget(outAct, target.Struct());

                var acRangeCheck = ActionManager.GetActionInRangeOrLoS(outAct, player.GameObject(), target is null ? player.GameObject() : target.Struct());
                var inRange = acRangeCheck is 0 or 565 || canUseSelf;

                var canUse = (canUseSelf || canUseTarget || areaTargeted) && AutoRotCanPressAction(outAct);
                var isHeal = attributes.AutoAction!.IsHeal;

                if (target is not null)
                {
                    if ((!isHeal && cfg.DPSSettings.DPSAlwaysHardTarget && mode is not DPSRotationMode.Manual) || (isHeal && cfg.HealerSettings.HealerAlwaysHardTarget && mode is not HealerRotationMode.Manual))
                        Svc.Targets.Target = target;
                }

                var castTime = ActionManager.GetAdjustedCastTime(ActionType.Action, outAct);
                bool orbwalking = cfg.OrbwalkerIntegration && OrbwalkerIPC.CanOrbwalk;
                if (TimeMoving.TotalMilliseconds > 0 && castTime > 0 && !orbwalking)
                    return false;

                if (canUse && (inRange || areaTargeted))
                {
                    var targetId = canUseTarget || areaTargeted ? target.GameObjectId : canUseSelf ? player.GameObjectId : 0xE000_0000;
                    var changed = CheckForChangedTarget(gameAct, ref targetId, out var replacedWith);
                    WouldLikeToGroundTarget = ActionSheet[outAct].TargetArea;
                    var ret = UseAutorotAction(ActionType.Action, Service.Configuration.ActionChanging ? gameAct : outAct, targetId);
                    WouldLikeToGroundTarget = false;

                    if (NIN.MudraSigns.Contains(outAct))
                        _lockedST = true;
                    else
                        _lockedST = false;

                    issued = true;
                    return true;
                }
            }
            finally
            {
                if (!issued)
                    OverrideTarget = null;
            }

            return false;
        }

        private static bool SwitchOnDChole(PresetStorage.PresetData attributes, uint outAct, ref IGameObject? newtarget)
        {
            if (outAct is SGE.Druochole && !attributes.AutoAction!.IsHeal)
            {
                if (GetPartyMembers()
                    .Where(x => !x.BattleChara.IsDead &&
                                x.BattleChara.IsTargetable &&
                                GetTargetDistance(x.BattleChara) <= QueryRange &&
                                IsInLineOfSight(x.BattleChara))
                    .OrderBy(x => GetTargetHPPercent(x.BattleChara))
                    .Select(x => x.BattleChara)
                    .TryGetFirst(out newtarget))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Matches <see cref="CanQueue" /> weave timing so oGCDs are not rejected while a short animation lock remains.
        /// </summary>
        internal static bool AutoRotCanPressAction(uint outAct) =>
            outAct.ActionAttackType() is { } type &&
            ((type is ActionAttackType.Ability && AnimationLock <= BaseActionQueue) ||
             (type is not ActionAttackType.Ability && RemainingGCD <= cfg.QueueWindow));

        private static bool CanUseAutorotDpsAction(uint outAct)
        {
            if (Player.Job is not Job.SGE)
                return true;

            return AutorotActionPolicy.AllowedInDpsLane(outAct, isSge: true);
        }

        public static uint InvokeCombo(Preset preset, PresetStorage.PresetData attributes, ref uint originalAct, IGameObject? optionalTarget = null, bool selectingAutorotAction = false)
        {
            if (attributes.ReplaceSkill is null) return originalAct;
            var outAct = attributes.ReplaceSkill.ActionIDs.FirstOrDefault();
            var customReplaceType = CustomActionHelper.GetTypeByAttribute(attributes.AutoAction!);
            var customReplaced = CustomActionHelper.CustomActionEnabled(customReplaceType);
            var customCombo = Service.ActionReplacer.CustomCombos.FirstOrDefault(x => x.Preset == preset);

            IsSelectingAutorotAction = selectingAutorotAction;
            try
            {
                foreach (var act in attributes.ReplaceSkill.ActionIDs)
                {
                    var actToCheck = customReplaced ? CustomActionHelper.GetActionId(customReplaceType) : act;

                    if (customCombo != null)
                    {
                        if (customCombo.TryInvoke(actToCheck, out var changedAct, optionalTarget))
                        {
                            originalAct = actToCheck;
                            outAct = changedAct;
                            Service.ActionReplacer.LastActionInvokeFor[actToCheck] = outAct;
                            break;
                        }
                    }
                }
            }
            finally
            {
                IsSelectingAutorotAction = false;
            }

            return outAct;
        }
    }

    public class DPSTargeting
    {
        private static bool Query(IGameObject x) =>
            x is IBattleChara chara &&
            !chara.IsDead &&
            GetTargetCurrentHP(chara, true) > 0 &&
            chara.IsTargetable &&
            chara.IsHostile() &&
            IsInRange(chara, InBossEncounter() && cfg.DPSSettings.IgnoreRangeInBoss ? 50f : cfg.DPSSettings.MaxDistance) &&
            GetTargetHeightDifference(chara) <= (InBossEncounter() && cfg.DPSSettings.IgnoreRangeInBoss ? 100f : cfg.DPSSettings.MaxDistance) &&
            !TargetIsInvincible(chara) &&
            !Service.Configuration.IgnoredNPCs.ContainsKey(chara.BaseId) &&
            ((cfg.DPSSettings.OnlyAttackInCombat && chara.Struct()->InCombat) || !cfg.DPSSettings.OnlyAttackInCombat) &&
            IsInLineOfSight(chara);

        public static IEnumerable<IGameObject> BaseSelection => Svc.Objects.Any(x => Query(x) && IsPriority(x))
            ? Svc.Objects.Where(x => Query(x) && IsPriority(x))
            : Svc.Objects.Where(x => Query(x));

        private static bool IsPriority(IGameObject x)
        {
            if (x is IBattleChara chara)
            {
                bool isFate = cfg.DPSSettings.FATEPriority && x.Struct()->FateId != 0 && InFATE();
                bool isQuest = cfg.DPSSettings.QuestPriority && IsQuestMob(x);

                return isFate || isQuest;
            }
            return false;
        }

        public static bool IsCombatPriority(IGameObject x)
        {
            if (x is IBattleChara chara)
            {
                if (!cfg.DPSSettings.PreferNonCombat) return true;
                bool inCombat = cfg.DPSSettings.PreferNonCombat && !chara.Struct()->InCombat;
                return inCombat;
            }
            return false;
        }

        public static IGameObject? GetTankTarget()
        {
            var tank = GetPartyMembers().FirstOrDefault(x => x.BattleChara?.GetRole() == CombatRole.Tank || HasStatusEffect(3615, x.BattleChara, true));
            if (tank == null)
                return null;

            return tank.BattleChara.TargetObject;
        }

        public static IGameObject? GetNearestTarget()
        {
            return BaseSelection
                .OrderByDescending(x => IsCombatPriority(x))
                .ThenBy(x => GetTargetDistance(x))
                .FirstOrDefault();
        }

        public static IGameObject? GetFurthestTarget()
        {
            return BaseSelection
                .OrderByDescending(x => IsCombatPriority(x))
                .ThenByDescending(x => GetTargetDistance(x))
                .FirstOrDefault();
        }

        public static IGameObject? GetLowestCurrentTarget()
        {
            return BaseSelection
                .OrderByDescending(x => IsCombatPriority(x))
                .ThenBy(x => GetTargetCurrentHP(x))
                .FirstOrDefault();
        }

        public static IGameObject? GetHighestCurrentTarget()
        {
            return BaseSelection
                .OrderByDescending(x => IsCombatPriority(x))
                .ThenByDescending(x => GetTargetCurrentHP(x))
                .FirstOrDefault();
        }

        public static IGameObject? GetLowestMaxTarget()
        {

            return BaseSelection
                .OrderByDescending(x => IsCombatPriority(x))
                .ThenBy(x => GetTargetMaxHP(x))
                .ThenBy(x => GetTargetHPPercent(x))
                .ThenBy(x => GetTargetDistance(x))
                .FirstOrDefault();
        }

        public static IGameObject? GetHighestMaxTarget()
        {
            return BaseSelection
                .OrderByDescending(x => IsCombatPriority(x))
                .ThenByDescending(x => GetTargetMaxHP(x))
                .ThenBy(x => GetTargetHPPercent(x))
                .ThenBy(x => GetTargetDistance(x))
                .FirstOrDefault();
        }
    }

    public static class HealerTargeting
    {
        internal static IGameObject? ManualTarget()
        {
            if (Svc.Targets.Target == null) return null;
            var t = Svc.Targets.Target;
            bool goodToHeal = t is IBattleChara &&
                              t.IsFriendly() &&
                              GetTargetHPPercent(t) <=
                              (TargetHasExcog(t) ? cfg.HealerSettings.SingleTargetExcogHPP :
                                  TargetHasRegen(t) ? cfg.HealerSettings.SingleTargetRegenHPP :
                                  cfg.HealerSettings.SingleTargetHPP);
            if (goodToHeal && !t.IsHostile())
            {
                return t;
            }
            return null;
        }
        internal static IGameObject? GetHighestCurrent()
        {
            if (GetPartyMembers().Count == 0)
                return PlayerNeedsSingleTargetHeal() ? Player.Object : null;
            return HealTargets().ThenByDescending(x => GetTargetHPPercent(x)).FirstOrDefault();
        }

        internal static IGameObject? GetLowestCurrent()
        {
            if (GetPartyMembers().Count == 0)
                return PlayerNeedsSingleTargetHeal() ? Player.Object : null;
            return HealTargets().ThenBy(x => GetTargetHPPercent(x)).FirstOrDefault();
        }

        private static bool PlayerNeedsSingleTargetHeal()
        {
            if (Player.Object is null)
                return false;

            return GetTargetHPPercent(Player.Object, cfg.HealerSettings.IncludeShields) <=
                   (TargetHasExcog(Player.Object) ? cfg.HealerSettings.SingleTargetExcogHPP :
                       TargetHasRegen(Player.Object) ? cfg.HealerSettings.SingleTargetRegenHPP :
                       cfg.HealerSettings.SingleTargetHPP);
        }

        internal static IOrderedEnumerable<IGameObject?> HealTargets()
        {
            return GetPartyMembers()
                .Where(x => !x.BattleChara.IsDead &&
                            x.BattleChara.IsTargetable &&
                            GetTargetDistance(x.BattleChara) <= QueryRange &&
                            !TargetHasImmortality(x.BattleChara) &&
                            !x.BattleChara.StatusList.Any(x => StatusCache.DoNotHealStatuses.Contains(x.StatusId)) &&
                            GetTargetHPPercent(x.BattleChara, cfg.HealerSettings.IncludeShields) <=
                            (TargetHasExcog(x.BattleChara) ? cfg.HealerSettings.SingleTargetExcogHPP :
                                TargetHasRegen(x.BattleChara) ? cfg.HealerSettings.SingleTargetRegenHPP :
                                cfg.HealerSettings.SingleTargetHPP) &&
                            IsInLineOfSight(x.BattleChara))
                .Select(x => x.BattleChara)
                .OrderBy(x => TargetHasTrueInvuln(x));
        }

        internal static bool CanAoEHeal(uint outAct = 0)
        {
            int memberCount;
            try
            {
                var members = GetPartyMembers()
                    .Where(x => x.BattleChara is not null &&
                                !x.BattleChara.IsDead &&
                                x.BattleChara.IsTargetable &&
                                !x.IsOutOfPartyNPC &&
                                !x.BattleChara.StatusList.Any(x => StatusCache.DoNotHealStatuses.Contains(x.StatusId)) &&
                                (outAct == 0
                                    ? GetTargetDistance(x.BattleChara) <= 20f
                                    : InActionRange(outAct, x.BattleChara)) &&
                                GetTargetHPPercent(x.BattleChara, cfg.HealerSettings.IncludeShields) <= cfg.HealerSettings.AoETargetHPP);
                memberCount = members.Count();
            }
            catch { memberCount = 0; }

            if (memberCount < cfg.HealerSettings.AoEHealTargetCount)
                return false;

            return true;
        }

        private static bool TargetHasRegen(IGameObject? target)
        {
            if (target is null) return false;
            return JobID switch
            {
                Job.AST => HasStatusEffect(AST.Buffs.AspectedBenefic, target),
                Job.WHM => HasStatusEffect(WHM.Buffs.Regen, target),
                _ => false,
            };
        }
        private static bool TargetHasExcog(IGameObject? target)
        {
            return target is not null && HasStatusEffect(SCH.Buffs.Excogitation, target, true);
        }
        /// Used to skip the healing of tanks that are invuln but still receive damage
        private static bool TargetHasImmortality(IGameObject? target)
        {
            if (target is null) return false;

            return GetStatusEffectRemainingTime(DRK.Buffs.LivingDead, target, true) >= 3 ||
                   GetStatusEffectRemainingTime(DRK.Buffs.WalkingDead, target, true) >= 5 ||
                   GetStatusEffectRemainingTime(WAR.Buffs.Holmgang, target, true) >= 5;
        }
        /// Used to de-prioritize (not skip) the healing of invuln tanks
        private static bool TargetHasTrueInvuln(IGameObject? target)
        {
            if (target is null) return false;

            return GetStatusEffectRemainingTime(GNB.Buffs.Superbolide, target) >= 5 ||
                   GetStatusEffectRemainingTime(PLD.Buffs.HallowedGround, target) >= 5;
        }
    }

    public static class TankTargeting
    {
        public static IGameObject? GetLowestCurrentTarget()
        {
            return DPSTargeting.BaseSelection
                .OrderByDescending(x => DPSTargeting.IsCombatPriority(x))
                .ThenByDescending(x => x.TargetObject?.GameObjectId != Player.Object?.GameObjectId)
                .ThenBy(x => GetTargetCurrentHP(x))
                .ThenBy(x => GetTargetHPPercent(x)).FirstOrDefault();
        }

        public static IGameObject? GetHighestCurrentTarget()
        {
            return DPSTargeting.BaseSelection
                .OrderByDescending(x => DPSTargeting.IsCombatPriority(x))
                .ThenByDescending(x => x.TargetObject?.GameObjectId != Player.Object?.GameObjectId)
                .ThenByDescending(x => GetTargetCurrentHP(x))
                .ThenBy(x => GetTargetHPPercent(x)).FirstOrDefault();
        }

        public static IGameObject? GetLowestMaxTarget()
        {
            var t = DPSTargeting.BaseSelection
                .OrderByDescending(x => DPSTargeting.IsCombatPriority(x))
                .ThenByDescending(x => x.TargetObject?.GameObjectId != Player.Object?.GameObjectId)
                .ThenBy(x => GetTargetMaxHP(x))
                .ThenBy(x => GetTargetHPPercent(x)).FirstOrDefault();

            return t;
        }

        public static IGameObject? GetHighestMaxTarget()
        {
            return DPSTargeting.BaseSelection
                .OrderByDescending(x => DPSTargeting.IsCombatPriority(x))
                .ThenByDescending(x => x.TargetObject?.GameObjectId != Player.Object?.GameObjectId)
                .ThenByDescending(x => GetTargetMaxHP(x))
                .ThenBy(x => GetTargetHPPercent(x)).FirstOrDefault();
        }
    }
}
