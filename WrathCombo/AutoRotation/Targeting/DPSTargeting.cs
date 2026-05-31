#region

using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using System.Collections.Generic;
using System.Linq;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

#endregion

namespace WrathCombo.AutoRotation;

public unsafe class DPSTargeting
{
    private static bool Query(IGameObject x) =>
        x is IBattleChara chara &&
        !chara.IsDead &&
        chara.IsTargetable &&
        chara.IsHostile() &&
        IsInRange(chara, InBossEncounter() && AutoRotationController.cfg!.DPSSettings.IgnoreRangeInBoss ? 50f : AutoRotationController.cfg!.DPSSettings.MaxDistance) &&
        GetTargetHeightDifference(chara) <= (InBossEncounter() && AutoRotationController.cfg!.DPSSettings.IgnoreRangeInBoss ? 100f : AutoRotationController.cfg!.DPSSettings.MaxDistance) &&
        !TargetIsInvincible(chara) &&
        !Services.Service.Configuration.IgnoredNPCs.ContainsKey(chara.BaseId) &&
        ((AutoRotationController.cfg!.DPSSettings.OnlyAttackInCombat && chara.Struct()->InCombat) || !AutoRotationController.cfg!.DPSSettings.OnlyAttackInCombat) &&
        IsInLineOfSight(chara);

    public static IEnumerable<IGameObject> BaseSelection => Svc.Objects.Any(x => Query(x) && IsPriority(x))
        ? Svc.Objects.Where(x => Query(x) && IsPriority(x))
        : Svc.Objects.Where(x => Query(x));

    private static bool IsPriority(IGameObject x)
    {
        if (x is IBattleChara)
        {
            bool isFate = AutoRotationController.cfg!.DPSSettings.FATEPriority && x.Struct()->FateId != 0 && InFATE();
            bool isQuest = AutoRotationController.cfg!.DPSSettings.QuestPriority && IsQuestMob(x);

            return isFate || isQuest;
        }
        return false;
    }

    public static bool IsCombatPriority(IGameObject x)
    {
        if (x is IBattleChara chara)
        {
            if (!AutoRotationController.cfg!.DPSSettings.PreferNonCombat) return true;
            bool inCombat = AutoRotationController.cfg!.DPSSettings.PreferNonCombat && !chara.Struct()->InCombat;
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

    public static IGameObject? GetLowestCurrentTarget() => GetCurrentTarget(lowestFirst: true);
    public static IGameObject? GetHighestCurrentTarget() => GetCurrentTarget(lowestFirst: false);

    private static IGameObject? GetCurrentTarget(bool lowestFirst)
    {
        var ordered = BaseSelection.OrderByDescending(x => IsCombatPriority(x));
        return lowestFirst
            ? ordered.ThenBy(x => GetTargetCurrentHP(x)).FirstOrDefault()
            : ordered.ThenByDescending(x => GetTargetCurrentHP(x)).FirstOrDefault();
    }

    public static IGameObject? GetLowestMaxTarget()
    {
        return BaseSelection
            .OrderByDescending(x => IsCombatPriority(x))
            .ThenBy(x => GetTargetMaxHP(x))
            .ThenBy(x => GetTargetHPPercent(x))
            .FirstOrDefault();
    }

    public static IGameObject? GetHighestMaxTarget()
    {
        return BaseSelection
            .OrderByDescending(x => IsCombatPriority(x))
            .ThenByDescending(x => GetTargetMaxHP(x))
            .ThenBy(x => GetTargetHPPercent(x))
            .FirstOrDefault();
    }
}
