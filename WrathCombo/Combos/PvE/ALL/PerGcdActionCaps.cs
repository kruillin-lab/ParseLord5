using System;
using System.Linq;
using WrathCombo.Data;

namespace WrathCombo.Combos.PvE;

internal static class PerGcdActionCaps
{
    internal static bool AnyUsed(Func<uint, bool> isCappedAction) =>
        ActionWatching.WeaveActions.Any(isCappedAction);

    internal static bool ShouldHold(uint actionId, Func<uint, bool> isCappedAction) =>
        isCappedAction(actionId) && AnyUsed(isCappedAction);
}
