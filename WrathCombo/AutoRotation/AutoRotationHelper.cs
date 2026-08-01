#region

using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Linq;
using System.Numerics;
using WrathCombo.API.Enum;
using WrathCombo.Combos.PvE;
using WrathCombo.Combos.PvE.Enums;
using WrathCombo.Core;
using WrathCombo.CustomComboNS;
using WrathCombo.Data;
using WrathCombo.Extensions;
using WrathCombo.Services;
using WrathCombo.Services.IPC_Subscriber;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;
using static WrathCombo.Data.ActionWatching;
using ActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

#endregion

namespace WrathCombo.AutoRotation;

internal unsafe partial class AutoRotationController
{
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
                        DPSRotationMode.Manual => Svc.Targets.Target,
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
                        DPSRotationMode.Manual => Svc.Targets.Target,
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

            if (attributes.AutoAction!.IsHeal)
            {
                LockedAoE = false;
                LockedST = false;

                uint outAct = OriginalHook(InvokeCombo(preset, attributes, ref gameAct, Player.Object));
                if (!ActionReady(outAct))
                    return false;

                var canQueue = outAct.ActionAttackType() is { } type && (type is ActionAttackType.Ability || type is not ActionAttackType.Ability && RemainingGCD <= cfg!.QueueWindow);
                if (!canQueue)
                    return false;

                if (HealerTargeting.CanAoEHeal(outAct))
                {
                    var castTime = ActionManager.GetAdjustedCastTime(ActionType.Action, outAct);
                    bool orbwalking = cfg!.OrbwalkerIntegration && OrbwalkerIPC.CanOrbwalk;
                    if (TimeMoving.TotalMilliseconds > 0 && castTime > 0 && !orbwalking)
                        return false;

                    var targetId = player.GameObjectId;
                    var changed = CheckForChangedTarget(gameAct, ref targetId, out var replacedWith);
                    WouldLikeToGroundTarget = ActionSheet[outAct].TargetArea;
                    var ret = ActionManager.Instance()->UseAction(ActionType.Action, Service.Configuration.ActionChanging ? gameAct : outAct, targetId);
                    WouldLikeToGroundTarget = false;

                    return true;
                }
            }
            else
            {
                var target = !cfg!.DPSSettings.AoEIgnoreManual && cfg.DPSRotationMode == DPSRotationMode.Manual ?
                    Svc.Targets.Target : DPSTargeting.BaseSelection.MaxBy(x => NumberOfEnemiesInRange(OriginalHook(gameAct), x, true));

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
                OverrideTarget = target;
                uint outAct = OriginalHook(InvokeCombo(preset, attributes, ref gameAct, target));
                if (outAct is All.SavageBlade) return true;
                if (!ActionReady(outAct))
                {
                    OverrideTarget = null;
                    return false;
                }

                var canQueue = outAct.ActionAttackType() is { } type && ((type is ActionAttackType.Ability && AnimationLock == 0) || (type is not ActionAttackType.Ability && RemainingGCD <= cfg.QueueWindow));
                if (!canQueue)
                {
                    OverrideTarget = null;
                    return false;
                }
                var sheet = ActionSheet[outAct];
                var targetsHostile = sheet.CanTargetHostile;

                bool switched = SwitchOnDChole(attributes, outAct, ref target);
                var castTime = ActionManager.GetAdjustedCastTime(ActionType.Action, outAct);
                bool orbwalking = cfg.OrbwalkerIntegration && OrbwalkerIPC.CanOrbwalk;
                if (TimeMoving.TotalMilliseconds > 0 && castTime > 0 && !orbwalking)
                {
                    OverrideTarget = null;
                    return false;
                }

                if (cfg.DPSSettings.DPSAlwaysHardTarget && target != null)
                    Svc.Targets.Target = target;

                var canUseSelf = sheet.CanTargetSelf;
                var areaTargeted = ActionSheet[outAct].TargetArea;
                var acRangeCheck = ActionManager.GetActionInRangeOrLoS(outAct, player.GameObject(), target is null ? player.GameObject() : target.Struct());
                var inRange = acRangeCheck is 0 or 565 || canUseSelf || areaTargeted;

                if (targetsHostile && target is not null)
                {
                    Svc.GameConfig.TryGet(Dalamud.Game.Config.UiControlOption.AutoFaceTargetOnAction, out uint original);
                    Svc.GameConfig.Set(Dalamud.Game.Config.UiControlOption.AutoFaceTargetOnAction, 1);
                    Vector3 pos = new(Player.Object.Position.X, Player.Object.Position.Y, Player.Object.Position.Z);
                    ActionManager.Instance()->AutoFaceTargetPosition(&pos, target.GameObjectId);
                    Svc.GameConfig.Set(Dalamud.Game.Config.UiControlOption.AutoFaceTargetOnAction, original);
                }

                if (inRange)
                {
                    //Chance target of target.GameObjectID can be null
                    var targetId = (targetsHostile && target != null) || switched ? target.GameObjectId : canUseSelf ? player.GameObjectId : 0xE000_0000;
                    var changed = CheckForChangedTarget(gameAct, ref targetId, out var replacedWith);
                    WouldLikeToGroundTarget = areaTargeted;
                    var ret = ActionManager.Instance()->UseAction(ActionType.Action, Service.Configuration.ActionChanging ? gameAct : outAct, targetId);
                    WouldLikeToGroundTarget = false;
                    if (NIN.MudraSigns.Contains(outAct))
                        _lockedAoE = true;
                    else
                        _lockedAoE = false;

                    return true;
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
            OverrideTarget = target;
            var outAct = OriginalHook(InvokeCombo(preset, attributes, ref gameAct, target));
            if (!CanQueue(outAct))
            {
                return false;
            }

            bool switched = SwitchOnDChole(attributes, outAct, ref target);
            if (outAct is DNC.ClosedPosition && DNC.DancePartnerResolver() is IBattleChara dp)
                target = dp;

            var canUseSelf = NIN.MudraSigns.Contains(outAct)
                ? target is not null && target.IsHostile()
                : ActionManager.CanUseActionOnTarget(outAct, Player.GameObject);

            var blockedSelfBuffs = GetCooldown(outAct).CooldownTotal >= 5;

            if (cfg!.InCombatOnly && NotInCombat && !CombatBypass && !(canUseSelf && cfg.BypassBuffs && !blockedSelfBuffs))
            {
                OverrideTarget = null;
                return false;
            }

            if (target is null && !canUseSelf)
            {
                OverrideTarget = null;
                return false;
            }

            var areaTargeted = ActionSheet[outAct].TargetArea;
            var canUseTarget = target is not null && ActionManager.CanUseActionOnTarget(outAct, target.Struct());

            var acRangeCheck = ActionManager.GetActionInRangeOrLoS(outAct, player.GameObject(), target is null ? player.GameObject() : target.Struct());
            var inRange = acRangeCheck is 0 or 565 || canUseSelf;

            var canUse = (canUseSelf || canUseTarget || areaTargeted) && outAct.ActionAttackType() is { } type && ((type is ActionAttackType.Ability && AnimationLock == 0) || (type is not ActionAttackType.Ability && RemainingGCD <= cfg.QueueWindow));
            var isHeal = attributes.AutoAction!.IsHeal;

            if ((!isHeal && cfg.DPSSettings.DPSAlwaysHardTarget && mode is not DPSRotationMode.Manual) || (isHeal && cfg.HealerSettings.HealerAlwaysHardTarget && mode is not HealerRotationMode.Manual) && target != null)
                Svc.Targets.Target = target;

            var castTime = ActionManager.GetAdjustedCastTime(ActionType.Action, outAct);
            bool orbwalking = cfg.OrbwalkerIntegration && OrbwalkerIPC.CanOrbwalk;
            if (TimeMoving.TotalMilliseconds > 0 && castTime > 0 && !orbwalking)
            {
                OverrideTarget = null;
                return false;
            }

            if (canUse && (inRange || areaTargeted))
            {
                var targetId = canUseTarget || areaTargeted ? target.GameObjectId : canUseSelf ? player.GameObjectId : 0xE000_0000;
                var changed = CheckForChangedTarget(gameAct, ref targetId, out var replacedWith);
                WouldLikeToGroundTarget = ActionSheet[outAct].TargetArea;
                var ret = ActionManager.Instance()->UseAction(ActionType.Action, Service.Configuration.ActionChanging ? gameAct : outAct, targetId);
                WouldLikeToGroundTarget = false;

                if (NIN.MudraSigns.Contains(outAct))
                    _lockedST = true;
                else
                    _lockedST = false;

                return true;
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

        public static uint InvokeCombo(Preset preset, PresetStorage.PresetData attributes, ref uint originalAct, IGameObject? optionalTarget = null)
        {
            if (attributes.ReplaceSkill is null) return originalAct;
            var outAct = attributes.ReplaceSkill.ActionIDs.FirstOrDefault();
            foreach (var actToCheck in attributes.ReplaceSkill.ActionIDs)
            {
                var customCombo = Service.ActionReplacer.CustomCombos.FirstOrDefault(x => x.Preset == preset);
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

            return outAct;
        }
    }
}
