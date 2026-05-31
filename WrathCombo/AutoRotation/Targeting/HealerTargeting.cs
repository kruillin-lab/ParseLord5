#region

using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using System.Linq;
using WrathCombo.Combos.PvE;
using WrathCombo.Extensions;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;
using static WrathCombo.CustomComboNS.Functions.Jobs;

#endregion

namespace WrathCombo.AutoRotation;

public static class HealerTargeting
{
    internal static IGameObject? ManualTarget()
    {
        if (Svc.Targets.Target == null) return null;
        var t = Svc.Targets.Target;
        bool goodToHeal = t is IBattleChara &&
                          t.IsFriendly() &&
                          GetTargetHPPercent(t) <=
                          (TargetHasExcog(t) ? AutoRotationController.cfg!.HealerSettings.SingleTargetExcogHPP :
                              TargetHasRegen(t) ? AutoRotationController.cfg!.HealerSettings.SingleTargetRegenHPP :
                              AutoRotationController.cfg!.HealerSettings.SingleTargetHPP);
        if (goodToHeal && !t.IsHostile())
        {
            return t;
        }
        return null;
    }

    internal static IGameObject? GetHighestCurrent() => GetHealTarget(lowestFirst: false);
    internal static IGameObject? GetLowestCurrent() => GetHealTarget(lowestFirst: true);

    private static IGameObject? GetHealTarget(bool lowestFirst)
    {
        if (GetPartyMembers().Count == 0) return Player.Object;
        var candidates = GetPartyMembers()
            .Where(x => !x.BattleChara.IsDead &&
                        x.BattleChara.IsTargetable &&
                        GetTargetDistance(x.BattleChara) <= AutoRotationController.QueryRange &&
                        !TargetHasImmortality(x.BattleChara) &&
                        GetTargetHPPercent(x.BattleChara) <=
                        (TargetHasExcog(x.BattleChara) ? AutoRotationController.cfg!.HealerSettings.SingleTargetExcogHPP :
                            TargetHasRegen(x.BattleChara) ? AutoRotationController.cfg!.HealerSettings.SingleTargetRegenHPP :
                            AutoRotationController.cfg!.HealerSettings.SingleTargetHPP) &&
                        IsInLineOfSight(x.BattleChara))
            .OrderBy(x => TargetHasTrueInvuln(x.BattleChara));
        var target = lowestFirst
            ? candidates.ThenBy(x => GetTargetHPPercent(x.BattleChara)).FirstOrDefault()
            : candidates.ThenByDescending(x => GetTargetHPPercent(x.BattleChara)).FirstOrDefault();
        return target?.BattleChara;
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
                            (outAct == 0
                                ? GetTargetDistance(x.BattleChara) <= 20f
                                : InActionRange(outAct, x.BattleChara)) &&
                            GetTargetHPPercent(x.BattleChara) <= AutoRotationController.cfg!.HealerSettings.AoETargetHPP);
            memberCount = members.Count();
        }
        catch { memberCount = 0; }

        if (memberCount < AutoRotationController.cfg!.HealerSettings.AoEHealTargetCount)
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
