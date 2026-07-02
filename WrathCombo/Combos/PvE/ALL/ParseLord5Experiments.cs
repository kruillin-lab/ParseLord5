using WrathCombo.Services;

namespace WrathCombo.Combos.PvE;

/// <summary>
///     Call-site accessors for ParseLord5 experimental features: master switch
///     AND per-feature flag, so a single feature can be toggled off when
///     bisecting a regression without disabling every experiment at once.
/// </summary>
internal static class ParseLord5Experiments
{
    private static bool Master =>
        Service.Configuration.ParseLord5ExperimentalMode;

    internal static bool JobRotationExperiments =>
        Master && Service.Configuration.Experimental.JobRotationExperiments;

    internal static bool SmartMitigation =>
        Master && Service.Configuration.Experimental.SmartMitigation;

    internal static bool DynamicHealCurve =>
        Master && Service.Configuration.Experimental.DynamicHealCurve;

    internal static bool FastPartyCache =>
        Master && Service.Configuration.Experimental.FastPartyCache;

    internal static bool NoTargetDpsFallback =>
        Master && Service.Configuration.Experimental.NoTargetDpsFallback;

    internal static bool CombatTelemetry =>
        Master && Service.Configuration.Experimental.CombatTelemetry;
}
