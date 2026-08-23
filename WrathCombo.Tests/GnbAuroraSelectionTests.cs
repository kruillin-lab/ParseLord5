using WrathCombo.Services.SmartMitigation;

namespace WrathCombo.Tests;

public class GnbAuroraSelectionTests
{
    [Theory]
    [InlineData(90_000u, 100_000u, 90, true)]
    [InlineData(90_001u, 100_000u, 90, false)]
    [InlineData(99_000u, 100_000u, 99, true)]
    public void BossSelfHeal_UsesConfiguredHpThreshold(
        uint currentHp,
        uint maxHp,
        int hpThreshold,
        bool expected)
    {
        var result = GnbAuroraSelection.ShouldUseBossSelfHeal(
            enabled: true,
            canWeave: true,
            usedDefensiveThisGcd: false,
            actionReady: true,
            currentHp,
            maxHp,
            simpleMode: false,
            configuredHpThreshold: hpThreshold,
            auroraActive: false,
            justUsedAurora: false,
            catharsisOfCorundumActive: false);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(false, true, false, true, false, false, false)]
    [InlineData(true, false, false, true, false, false, false)]
    [InlineData(true, true, true, true, false, false, false)]
    [InlineData(true, true, false, false, false, false, false)]
    [InlineData(true, true, false, true, true, false, false)]
    [InlineData(true, true, false, true, false, true, false)]
    [InlineData(true, true, false, true, false, false, true)]
    public void BossSelfHeal_UnsafeContext_DoesNotUseAurora(
        bool enabled,
        bool canWeave,
        bool usedDefensiveThisGcd,
        bool actionReady,
        bool auroraActive,
        bool justUsedAurora,
        bool catharsisOfCorundumActive)
    {
        var result = GnbAuroraSelection.ShouldUseBossSelfHeal(
            enabled,
            canWeave,
            usedDefensiveThisGcd,
            actionReady,
            currentHp: 50_000,
            maxHp: 100_000,
            simpleMode: false,
            configuredHpThreshold: 90,
            auroraActive,
            justUsedAurora,
            catharsisOfCorundumActive);

        Assert.False(result);
    }

    [Fact]
    public void BossSelfHeal_UnknownMaxHp_DoesNotUseAurora()
    {
        var result = GnbAuroraSelection.ShouldUseBossSelfHeal(
            enabled: true,
            canWeave: true,
            usedDefensiveThisGcd: false,
            actionReady: true,
            currentHp: 0,
            maxHp: 0,
            simpleMode: false,
            configuredHpThreshold: 90,
            auroraActive: false,
            justUsedAurora: false,
            catharsisOfCorundumActive: false);

        Assert.False(result);
    }

    [Theory]
    [InlineData(90_000u, true)]
    [InlineData(90_001u, false)]
    public void BossSelfHeal_SimpleModeUsesNinetyPercentThreshold(uint currentHp, bool expected)
    {
        var result = GnbAuroraSelection.ShouldUseBossSelfHeal(
            enabled: true,
            canWeave: true,
            usedDefensiveThisGcd: false,
            actionReady: true,
            currentHp,
            maxHp: 100_000,
            simpleMode: true,
            configuredHpThreshold: 99,
            auroraActive: false,
            justUsedAurora: false,
            catharsisOfCorundumActive: false);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(90_000u)]
    [InlineData(80_000u)]
    [InlineData(50_000u)]
    public void BossSelfHeal_Blocked_When_CatharsisOfCorundumActive(uint currentHp)
    {
        var result = GnbAuroraSelection.ShouldUseBossSelfHeal(
            enabled: true,
            canWeave: true,
            usedDefensiveThisGcd: false,
            actionReady: true,
            currentHp,
            maxHp: 100_000,
            simpleMode: false,
            configuredHpThreshold: 99,
            auroraActive: false,
            justUsedAurora: false,
            catharsisOfCorundumActive: true);

        Assert.False(result);
    }
}
