namespace WrathCombo.Services.SmartMitigation;

/// <summary>
/// Shared trash-pack mitigation ordering for tank smart mit (WAR first; PLD/GNB T005 should reuse).
/// Party Reprisal is a short CD (60s recast — not long) and should fire before personal long CDs on trash.
/// Long-long rule: recast strictly greater than <see cref="LongMitigationRecastSeconds"/> cannot overlap
/// another long; job bridges should also classify by action id (e.g. Rampart/Arms Length stay short despite higher recast in data).
/// </summary>
internal static class TrashMitigationOrdering
{
    /// <summary>Threshold for long-long mutual exclusion (Reprisal at exactly 60s is short).</summary>
    public const float LongMitigationRecastSeconds = 60f;

    /// <summary>Reprisal recast (60s) — treated as short/party; does not block long personal mits when active.</summary>
    public const float ReprisalCooldownSeconds = 60f;

    /// <summary>Default minimum enemies in range before trash Reprisal is prioritized (WAR non-boss preset uses 3).</summary>
    public const int DefaultMinEnemiesForTrashReprisal = 3;

    /// <summary>Recast strictly &gt; 60s — long-long overlap forbidden. Exactly 60s (Reprisal) is not long.</summary>
    public static bool IsLongMitigationRecast(float cooldownSeconds) =>
        cooldownSeconds > LongMitigationRecastSeconds;

    /// <summary>When a long mitigation buff is active, exclude another long option from selection/fallbacks.</summary>
    public static bool ShouldExcludeLongMitigationOption(
        bool longMitigationBuffActive,
        bool optionIsLongMitigation) =>
        longMitigationBuffActive && optionIsLongMitigation;

    /// <summary>
    /// Trash packs: fire party Reprisal before personal long CDs when enabled and ready.
    /// Caller must already have validated trash context and mitigation threat.
    /// </summary>
    public static bool ShouldPrioritizeTrashReprisalFirst(
        bool isTrash,
        bool reprisalEnabled,
        bool reprisalReady,
        bool reprisalRecentlyUsed,
        int enemyCountInRange,
        int minEnemies = DefaultMinEnemiesForTrashReprisal) =>
        isTrash &&
        reprisalEnabled &&
        reprisalReady &&
        !reprisalRecentlyUsed &&
        enemyCountInRange >= minEnemies;
}
