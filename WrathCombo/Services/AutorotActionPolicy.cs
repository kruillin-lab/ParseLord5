using System.Collections.Frozen;

namespace WrathCombo.Services;

/// <summary>
///     ParseLord5: pure decision core for Auto-Rotation action resolution.
///     Owns the two rules that previously lived as scattered code in
///     AutoRotationController: which id an ActionStacksEX peek is keyed on,
///     and which resolved actions may never leave the DPS probe lane.
///     Dalamud-free by design so WrathCombo.Tests can exercise it directly
///     (linked source, same pattern as HealDelayCurve).
/// </summary>
internal static class AutorotActionPolicy
{
    /// <summary>
    ///     The id an ActionStacksEX peek must be keyed on: the raw game action
    ///     when action-changing hooks are enabled, the combo's choice otherwise.
    /// </summary>
    internal static uint PeekKey(uint gameAct, uint outAct, bool actionChanging) =>
        actionChanging ? gameAct : outAct;

    /// <summary>A successful peek replaces the combo's choice outright; otherwise the choice stands.</summary>
    internal static uint ResolveAction(uint outAct, bool redirected, uint resolvedAction) =>
        redirected ? resolvedAction : outAct;

    /// <summary>
    ///     SGE defensive and healing actions that must never fire from a DPS probe.
    ///     Mirrors SGE_Helper.cs action IDs. Eukrasia is deliberately absent:
    ///     it buffs the next GCD and belongs to the DPS lane.
    /// </summary>
    internal static readonly FrozenSet<uint> SgeBlockedInDpsLane = new[]
    {
        24285u, // Kardia
        24309u, // Rhizomata
        24294u, // Soteria
        24296u, // Druochole
        24303u, // Taurochole
        24305u, // Haima
        24317u, // Krasis
        24300u, // Zoe
        24301u, // Pepsis
        24298u, // Kerachole
        24299u, // Ixochole
        24310u, // Holos
        24311u, // Panhaima
        37035u, // Philosophia
        24288u, // Physis
        24302u, // Physis2
        24291u, // EukrasianDiagnosis
        24292u, // EukrasianPrognosis
        37034u, // EukrasianPrognosis2
    }.ToFrozenSet();

    /// <summary>Gate for the DPS probe lane. Non-SGE jobs are never blocked.</summary>
    internal static bool AllowedInDpsLane(uint finalAction, bool isSge) =>
        !isSge || !SgeBlockedInDpsLane.Contains(finalAction);
}
