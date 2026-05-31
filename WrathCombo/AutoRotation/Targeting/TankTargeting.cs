#region

using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using System.Linq;
using static WrathCombo.CustomComboNS.Functions.CustomComboFunctions;

#endregion

namespace WrathCombo.AutoRotation;

public static class TankTargeting
{
    public static IGameObject? GetLowestCurrentTarget() => GetCurrentTarget(lowestFirst: true);
    public static IGameObject? GetHighestCurrentTarget() => GetCurrentTarget(lowestFirst: false);

    private static IGameObject? GetCurrentTarget(bool lowestFirst)
    {
        var ordered = DPSTargeting.BaseSelection
            .OrderByDescending(x => DPSTargeting.IsCombatPriority(x))
            .ThenByDescending(x => x.TargetObject?.GameObjectId != Player.Object?.GameObjectId);
        return lowestFirst
            ? ordered.ThenBy(x => GetTargetCurrentHP(x)).ThenBy(x => GetTargetHPPercent(x)).FirstOrDefault()
            : ordered.ThenByDescending(x => GetTargetCurrentHP(x)).ThenBy(x => GetTargetHPPercent(x)).FirstOrDefault();
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
