#region

using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.DalamudServices;
using ECommons.DalamudServices.Legacy;
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
using WrathCombo.Extensions;
using WrathCombo.Services;
using WrathCombo.Services.IPC_Subscriber;
using WrathCombo.Window.Functions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;
using static WrathCombo.CustomComboNS.Functions.Jobs;
using static WrathCombo.Data.ActionWatching;
using ActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

#endregion

namespace WrathCombo.AutoRotation;

internal unsafe partial class AutoRotationController
{
    public static AutoRotationConfigIPCWrapper? cfg;

    public static long HealThrottle = 0;

    static bool _lockedST = false;
    static bool _lockedAoE = false;

    static DateTime? TimeToHeal;

    internal const float QueryRange = 30f;

    public static bool WouldLikeToGroundTarget;
    public static bool Paused;

    public static IGameObject? AutorotHealTarget;
    public static bool AutorotRaidwiding;
    public static int AutorotRaidwides = 0;
    public static bool TankbusterHandled = false;

    public AutoRotationController()
    {
        OnPartyCombatChanged += ResetError;
        OnStatusChanged += StatusChanged;
    }

    private void StatusChanged(uint statusId, bool onPlayer)
    {
        Svc.Log.Verbose($"[AutoRotStatusCheck] {((ushort)statusId).StatusName()} {(onPlayer ? "Gained" : "Lost")}");
    }

    public void Dispose()
    {
        OnPartyCombatChanged -= ResetError;
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
               || (ActionManager.Instance()->QueuedActionId > 0)
               || Paused;
    }

    internal static void Run()
    {
        cfg ??= new AutoRotationConfigIPCWrapper(Service.Configuration.RotationConfig);

        // Early exit for all conditions that should prevent autorotation
        if (ShouldSkipAutorotation())
            return;

        uint _ = 0;
        var autoActions = Presets.GetJobAutorots;

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

        // Healer logic
        bool isHealer = Player.Object?.Role is CombatRole.Healer;

        if (cfg.HealerSettings.HandleRaidwides)
        {
            if (isHealer && GroupDamageIncoming(out var multi))
            {
                AutorotRaidwiding = true;
                HandleRaidwide(multi);
            }
            else
            {
                if (AutorotRaidwides > 0)
                {
                    Svc.Log.Debug($"Used {AutorotRaidwides} raidwides {string.Join(", ", BlacklistedRaidwides.Select(x => x.ActionName()))}");
                    BlacklistedRaidwides.Clear();
                }
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
            return attr.AutoAction?.IsHeal == true && ActionReady(AutoRotationHelper.InvokeCombo(x.Key, attr, ref _));
        });

        bool canHeal = TimeToHeal is not null
                       && (DateTime.Now - TimeToHeal.Value).TotalSeconds >= cfg.HealerSettings.HealDelay
                       && actCheck;

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

    private static bool ProcessAutoActions(Dictionary<Preset, bool> autoActions, ref uint _, bool canHeal, bool stOnly)
    {
        // Two-pass iteration: AoE presets first, then ST presets.
        // Preserves the original OrderByDescending(IsAoE) ordering without allocating
        // anonymous wrapper objects or a sorted list.
        for (int pass = 0; pass < 2; pass++)
        {
            bool passIsAoE = pass == 0;

            // In stOnly mode, skip the AoE pass entirely.
            if (stOnly && passIsAoE)
                continue;

            foreach (var kvp in autoActions)
            {
                var attributes = kvp.Key.Attributes();
                var autoAction = attributes.AutoAction;
                var replaceSkill = attributes.ReplaceSkill;

                if (autoAction is null || replaceSkill is null)
                    continue;
                if (autoAction.IsHeal != canHeal)
                    continue;
                if (autoAction.IsAoE != passIsAoE)
                    continue;

                // Skip if locked
                if ((autoAction.IsAoE && LockedST) || (!autoAction.IsAoE && LockedAoE))
                    continue;

                // Skip if rez invuln is up
                if (!autoAction.IsHeal && HasStatusEffect(418))
                    continue;

                uint gameAct = replaceSkill.ActionIDs.First();
                var status = ActionManager.Instance()->GetActionStatus(ActionType.Action, gameAct, checkCastingActive: false, checkRecastActive: false);

                if (!LevelChecked(gameAct) || status == 581)
                    continue;

                if (autoAction.IsHeal)
                {
                    AutomateHealing(kvp.Key, attributes, gameAct);
                    continue;
                }

                // Tank logic
                if (Player.Object?.GetRole() is CombatRole.Tank)
                {
                    AutomateRotation(kvp.Key, attributes, gameAct);
                    continue;
                }

                // DPS logic
                if (AutomateRotation(kvp.Key, attributes, gameAct))
                    return false;
            }
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
                    ActionManager.Instance()->UseAction(ActionType.Action, regenSpell, SimpleTarget.FocusTarget.GameObjectId);
                    return;
                }
            }
        }
    }

    private static void PreEmptiveShield()
    {
        if (PartyInCombat() || SimpleTarget.FocusTarget is null || (InDuty() && !Svc.DutyState.IsDutyStarted))
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
                    ActionManager.Instance()->UseAction(ActionType.Action, prepSpell);
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
                    ActionManager.Instance()->UseAction(ActionType.Action, spell, SimpleTarget.FocusTarget.GameObjectId);
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
                    ActionManager.Instance()->UseAction(ActionType.Action, resSpell, member.BattleChara.GameObjectId);
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
                                ActionManager.Instance()->UseAction(ActionType.Action, RoleActions.Magic.Swiftcast);
                                return;
                            }
                        }
                    }

                    if (HasStatusEffect(RoleActions.Magic.Buffs.Swiftcast) || HasStatusEffect(RDM.Buffs.Dualcast) || !IsMoving())
                    {
                        ActionManager.Instance()->UseAction(ActionType.Action, resSpell, member.BattleChara.GameObjectId);
                        return;
                    }
                }

                if (Player.Job is Job.RDM)
                {
                    if (ActionReady(RoleActions.Magic.Swiftcast) && !HasStatusEffect(RDM.Buffs.Dualcast))
                    {
                        ActionManager.Instance()->UseAction(ActionType.Action, RoleActions.Magic.Swiftcast);
                        return;
                    }

                    if (ActionManager.GetAdjustedCastTime(ActionType.Action, resSpell) == 0)
                    {
                        ActionManager.Instance()->UseAction(ActionType.Action, resSpell, member.BattleChara.GameObjectId);
                    }

                }
                else
                {
                    if (ActionReady(RoleActions.Magic.Swiftcast))
                    {
                        if (ActionManager.Instance()->GetActionStatus(ActionType.Action, RoleActions.Magic.Swiftcast) == 0)
                        {
                            ActionManager.Instance()->UseAction(ActionType.Action, RoleActions.Magic.Swiftcast);
                            return;
                        }
                    }

                    if (!IsMoving() || HasStatusEffect(RoleActions.Magic.Buffs.Swiftcast))
                    {

                        if ((cfg is not null) && ((cfg.HealerSettings.AutoRezRequireSwift && ActionManager.GetAdjustedCastTime(ActionType.Action, resSpell) == 0) || !cfg.HealerSettings.AutoRezRequireSwift))
                        {
                            ActionManager.Instance()->UseAction(ActionType.Action, resSpell, member.BattleChara.GameObjectId);
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
                ActionManager.Instance()->UseAction(ActionType.Action, RoleActions.Healer.Esuna.Retarget(memberBC), memberBC.GameObjectId);
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
                ActionManager.Instance()->UseAction(ActionType.Action, SGE.Kardia.Retarget(member.BattleChara), member.BattleChara.GameObjectId);
                return;
            }
        }

    }

    private static bool AutomateRotation(Preset preset, PresetStorage.PresetData attributes, uint gameAct)
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
        if (Player.Object?.IsCasting() is true) return false;
        if (Environment.TickCount64 < HealThrottle) return false;

        if (attributes.AutoAction!.IsAoE)
        {
            var ret = AutoRotationHelper.ExecuteAoE(mode, preset, attributes, gameAct);
            return ret;
        }
        else
        {
            var ret = AutoRotationHelper.ExecuteST(mode, preset, attributes, gameAct);
            return ret;
        }
    }

}