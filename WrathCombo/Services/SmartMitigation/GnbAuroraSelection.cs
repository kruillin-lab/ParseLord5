namespace WrathCombo.Services.SmartMitigation;

internal static class GnbAuroraSelection
{
    internal static bool ShouldUseBossSelfHeal(
        bool enabled,
        bool canWeave,
        bool usedDefensiveThisGcd,
        bool actionReady,
        uint currentHp,
        uint maxHp,
        bool simpleMode,
        int configuredHpThreshold,
        bool auroraActive,
        bool justUsedAurora,
        bool catharsisOfCorundumActive)
    {
        if (!enabled ||
            !canWeave ||
            usedDefensiveThisGcd ||
            !actionReady ||
            maxHp == 0 ||
            auroraActive ||
            justUsedAurora ||
            catharsisOfCorundumActive)
            return false;

        var hpThreshold = simpleMode ? 90 : configuredHpThreshold;
        return (ulong)currentHp * 100 <= (ulong)maxHp * (uint)hpThreshold;
    }
}
