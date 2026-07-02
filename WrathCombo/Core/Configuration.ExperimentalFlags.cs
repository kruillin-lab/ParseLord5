using System;

namespace WrathCombo.Core;

public partial class Configuration
{
    /// <summary>
    ///     Per-feature toggles for ParseLord5 experimental behavior. Every flag
    ///     is AND-ed with <see cref="ParseLord5ExperimentalMode"/> at the call
    ///     site (via ParseLord5Experiments), so the master switch remains a
    ///     single kill-switch. Flags default to true to preserve the previous
    ///     all-on behavior for existing configs.
    /// </summary>
    public ExperimentalFlags Experimental = new();

    [Serializable]
    public class ExperimentalFlags
    {
        /// In-job priority reorders and boss-gating experiments (all jobs).
        public bool JobRotationExperiments = true;

        /// Tank smart mitigation selection (WAR/PLD/GNB/DRK).
        public bool SmartMitigation = true;

        /// HP-scaled healer reaction delay in Auto-Rotation.
        public bool DynamicHealCurve = true;

        /// 100ms party cache refresh while in combat.
        public bool FastPartyCache = true;

        /// Auto-Rotation Manual DPS mode falls back to a targeting mode when
        /// no hostile target is selected.
        public bool NoTargetDpsFallback = true;

        /// Rolling HP-delta telemetry feeding smart mitigation pressure state.
        public bool CombatTelemetry = true;
    }
}
