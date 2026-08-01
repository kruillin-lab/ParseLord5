using System.Collections.Generic;
using WrathCombo.Services.SmartMitigation;
using Xunit;

namespace WrathCombo.Tests;

public class TankMitigationSelectionTests
{
    [Theory]
    [InlineData(60f, false)]
    [InlineData(60.01f, true)]
    [InlineData(120f, true)]
    public void IsLongMitigationRecast_UsesStrictlyGreaterThanSixtySeconds(
        float cooldownSeconds,
        bool expected)
    {
        Assert.Equal(expected, TrashMitigationOrdering.IsLongMitigationRecast(cooldownSeconds));
    }

    [Fact]
    public void SelectMinimumMitigation_ActiveLongBlocksLongButAllowsShort()
    {
        var request = Request();
        var options = new List<MitigationOption>
        {
            new(ActionId: 1, DamageReduction: 0.70f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 90f, Tier: MitigationTier.Medium),
            new(ActionId: 2, DamageReduction: 0.70f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 60f, Tier: MitigationTier.Medium),
        };
        var active = new ActiveMitigationState(0f, 0f, 0f, false, LongMitigationActive: true);

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, active);

        Assert.Equal(2u, result!.Value.ActionId);
    }

    [Fact]
    public void SelectMinimumMitigation_ActiveShortBlocksShortButAllowsLong()
    {
        var request = Request();
        var options = new List<MitigationOption>
        {
            new(ActionId: 1, DamageReduction: 0.70f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 90f, Tier: MitigationTier.Medium),
            new(ActionId: 2, DamageReduction: 0.70f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 60f, Tier: MitigationTier.Medium),
        };
        var active = new ActiveMitigationState(0f, 0f, 0f, false, ShortMitigationActive: true);

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, active);

        Assert.Equal(1u, result!.Value.ActionId);
    }

    [Fact]
    public void SelectMinimumMitigation_BothCategoriesActive_ReturnsNull()
    {
        var request = Request();
        var options = new List<MitigationOption>
        {
            new(ActionId: 1, DamageReduction: 0.70f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 90f, Tier: MitigationTier.Medium),
            new(ActionId: 2, DamageReduction: 0.70f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 60f, Tier: MitigationTier.Medium),
        };
        var active = new ActiveMitigationState(0f, 0f, 0f, false, LongMitigationActive: true, ShortMitigationActive: true);

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, active);

        Assert.Null(result);
    }

    private static MitigationCoverageRequest Request() =>
        new(
            CurrentHp: 60_000,
            MaxHp: 100_000,
            IncomingDps: 20_000f,
            IncomingHps: 0f,
            MechanicSpikeFraction: 0f,
            HorizonSeconds: 4f,
            SafetyHpPercent: 0.30f);
}
