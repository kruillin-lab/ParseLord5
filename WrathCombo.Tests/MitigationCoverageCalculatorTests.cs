using System.Collections.Generic;
using WrathCombo.Services.SmartMitigation;
using Xunit;

namespace WrathCombo.Tests;

public class MitigationCoverageCalculatorTests
{
    private static readonly ActiveMitigationState NoActiveMit = new(0f, 0f, 0f, false);

    private static MitigationCoverageRequest Request(
        uint currentHp,
        uint maxHp,
        float incomingDps,
        float incomingHps = 0f,
        float mechanicSpikeFraction = 0f,
        float horizonSeconds = MitigationCoverageCalculator.DefaultHorizonSeconds,
        float safetyHpPercent = MitigationCoverageCalculator.DefaultSafetyHpPercent,
        bool confirmedTankbuster = false,
        float sustainMultiplier = 1f,
        bool preferHeavyMitigation = false) =>
        new(currentHp, maxHp, incomingDps, incomingHps, mechanicSpikeFraction, horizonSeconds,
            safetyHpPercent, confirmedTankbuster, sustainMultiplier, preferHeavyMitigation);

    [Fact]
    public void SelectMinimumMitigation_NoOptionsAvailable_ReturnsNull()
    {
        var request = Request(currentHp: 50_000, maxHp: 100_000, incomingDps: 50_000, mechanicSpikeFraction: 0.5f);

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(
            request, new List<MitigationOption>(), NoActiveMit);

        Assert.Null(result);
    }

    [Fact]
    public void SelectMinimumMitigation_MaxHpZero_ReturnsNull()
    {
        var request = Request(currentHp: 0, maxHp: 0, incomingDps: 50_000, mechanicSpikeFraction: 0.9f);
        var options = new List<MitigationOption>
        {
            new(ActionId: 1, DamageReduction: 0.2f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 30f, Tier: MitigationTier.Small),
        };

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, NoActiveMit);

        Assert.Null(result);
    }

    [Fact]
    public void SelectMinimumMitigation_InvulnAlreadyActive_ReturnsNull()
    {
        var request = Request(currentHp: 1, maxHp: 100_000, incomingDps: 1_000_000, mechanicSpikeFraction: 1f, confirmedTankbuster: true);
        var options = new List<MitigationOption>
        {
            new(ActionId: 99, DamageReduction: 0f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 300f, Tier: MitigationTier.Invuln),
        };
        var active = new ActiveMitigationState(0f, 0f, 0f, InvulnActive: true);

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, active);

        Assert.Null(result);
    }

    [Fact]
    public void SelectMinimumMitigation_NoIncomingDamage_ReturnsNull()
    {
        // netDps=0, spikeFraction=0 -> incomingBudget=0 -> early null before any option is considered.
        var request = Request(currentHp: 100_000, maxHp: 100_000, incomingDps: 0f, mechanicSpikeFraction: 0f);
        var options = new List<MitigationOption>
        {
            new(ActionId: 1, DamageReduction: 0.2f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 30f, Tier: MitigationTier.Small),
        };

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, NoActiveMit);

        Assert.Null(result);
    }

    [Fact]
    public void SelectMinimumMitigation_AlreadyAboveSafetyTarget_ReturnsNull()
    {
        // Full HP tank, small raidwide-tier spike: hpAfterHit stays comfortably above the 30% safety target.
        var request = Request(currentHp: 100_000, maxHp: 100_000, incomingDps: 0f,
            mechanicSpikeFraction: MitigationCoverageCalculator.RaidwideSpikeFraction);
        var options = new List<MitigationOption>
        {
            new(ActionId: 1, DamageReduction: 0.2f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 30f, Tier: MitigationTier.Small),
        };

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, NoActiveMit);

        Assert.Null(result);
    }

    [Fact]
    public void SelectMinimumMitigation_ModerateSpike_PicksLowestSufficientTier()
    {
        // 100k max HP, sustained net DPS over the 4s horizon puts hpAfterHit below the 30% safety target
        // with zero mitigation, and a Medium (40%) option closes the gap while a Large (60%) option would
        // over-mitigate — the scorer should prefer the lower tier when both satisfy coverage.
        var request = Request(currentHp: 100_000, maxHp: 100_000, incomingDps: 20_000f);
        var options = new List<MitigationOption>
        {
            new(ActionId: 10, DamageReduction: 0.40f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 60f, Tier: MitigationTier.Medium, Pool: MitigationPool.Exempt),
            new(ActionId: 20, DamageReduction: 0.60f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 90f, Tier: MitigationTier.Large, Pool: MitigationPool.Exempt),
        };

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, NoActiveMit);

        Assert.NotNull(result);
        Assert.Equal(10u, result!.Value.ActionId);
        Assert.StartsWith("tier=", result.Value.Reason);
    }

    [Fact]
    public void SelectMinimumMitigation_PreferHeavyMitigation_PicksLargeTierOverSmall()
    {
        var request = Request(currentHp: 100_000, maxHp: 100_000, incomingDps: 20_000f, preferHeavyMitigation: true);
        var options = new List<MitigationOption>
        {
            new(ActionId: 10, DamageReduction: 0.40f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 60f, Tier: MitigationTier.Medium, Pool: MitigationPool.Exempt),
            new(ActionId: 20, DamageReduction: 0.60f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 90f, Tier: MitigationTier.Large, Pool: MitigationPool.Exempt),
        };

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, NoActiveMit);

        Assert.NotNull(result);
        Assert.Equal(20u, result!.Value.ActionId);
    }

    [Fact]
    public void SelectMinimumMitigation_ConfirmedTankbusterWithLethalSpike_RequiresInvuln()
    {
        // Confirmed TB forces a 50% safety target; a near-full mechanic spike leaves hpAfterHit <= 0,
        // which alone satisfies the invuln branch's "hpAfterHit <= 0f" condition.
        var request = Request(currentHp: 100_000, maxHp: 100_000, incomingDps: 0f,
            mechanicSpikeFraction: 1.0f, confirmedTankbuster: true);
        var options = new List<MitigationOption>
        {
            new(ActionId: 99, DamageReduction: 0f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 300f, Tier: MitigationTier.Invuln),
        };

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, NoActiveMit);

        Assert.NotNull(result);
        Assert.Equal(99u, result!.Value.ActionId);
        Assert.Equal("invuln_required", result.Value.Reason);
    }

    [Fact]
    public void SelectMinimumMitigation_ConfirmedTankbusterNoOptionCoversGap_FallsBackToTbGuarantee()
    {
        // Confirmed TB with a spike no single Small/Medium option can close (score loop finds nothing),
        // but a Small option still exists — the TB-guarantee fallback should grab it rather than return null.
        var request = Request(currentHp: 100_000, maxHp: 100_000, incomingDps: 0f,
            mechanicSpikeFraction: 0.95f, confirmedTankbuster: true);
        var options = new List<MitigationOption>
        {
            new(ActionId: 11, DamageReduction: 0.10f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 30f, Tier: MitigationTier.Small),
        };

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, NoActiveMit);

        Assert.NotNull(result);
        Assert.Equal(11u, result!.Value.ActionId);
        Assert.Equal("tb_guarantee", result.Value.Reason);
    }

    [Fact]
    public void SelectMinimumMitigation_ConfirmedTankbusterFallback_ExcludesActiveSamePool()
    {
        var request = Request(currentHp: 100_000, maxHp: 100_000, incomingDps: 0f,
            mechanicSpikeFraction: 0.95f, confirmedTankbuster: true);
        var options = new List<MitigationOption>
        {
            new(ActionId: 11, DamageReduction: 0.10f, ShieldPotency: 0f, MaxHpBonusFraction: 0f,
                CooldownSeconds: 30f, Tier: MitigationTier.Small, Pool: MitigationPool.Long),
        };
        var active = new ActiveMitigationState(0f, 0f, 0f, InvulnActive: false, LongPoolActive: true);

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, active, isBoss: true);

        Assert.Null(result);
    }

    [Fact]
    public void SelectMinimumMitigation_ConfirmedTankbusterFallback_ExcludesTrashOnlyOnBoss()
    {
        var request = Request(currentHp: 100_000, maxHp: 100_000, incomingDps: 0f,
            mechanicSpikeFraction: 0.95f, confirmedTankbuster: true);
        var options = new List<MitigationOption>
        {
            new(ActionId: 11, DamageReduction: 0.10f, ShieldPotency: 0f, MaxHpBonusFraction: 0f,
                CooldownSeconds: 30f, Tier: MitigationTier.Small, Pool: MitigationPool.TrashOnly),
        };

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, NoActiveMit, isBoss: true);

        Assert.Null(result);
    }

    [Fact]
    public void DrkBossTankbuster_HighHp_SelectsEligiblePersonalMitigation()
    {
        var request = Request(
            currentHp: 95_000,
            maxHp: 100_000,
            incomingDps: 0f,
            mechanicSpikeFraction: 0.80f,
            confirmedTankbuster: true);
        var options = new List<MitigationOption>
        {
            new(ActionId: 3636, DamageReduction: 0.50f, ShieldPotency: 0f, MaxHpBonusFraction: 0f,
                CooldownSeconds: 120f, Tier: MitigationTier.Large, Pool: MitigationPool.Long),
        };

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(
            request, options, NoActiveMit, isBoss: true);

        Assert.NotNull(result);
        Assert.Equal(3636u, result!.Value.ActionId);
        Assert.StartsWith("tier=", result.Value.Reason);
    }

    [Fact]
    public void SelectMinimumMitigation_ReductionGapNegligibleAndNotConfirmedTb_ReturnsNull()
    {
        // Active mitigation already covers all but a sliver of the required reduction (<=1%),
        // and this isn't a confirmed tankbuster, so the calculator should decline to add more.
        var request = Request(currentHp: 60_000, maxHp: 100_000, incomingDps: 10_000f);
        var active = new ActiveMitigationState(CombinedDamageReduction: 0.99f, ActiveShield: 0f, ActiveMaxHpBonusFraction: 0f, InvulnActive: false);
        var options = new List<MitigationOption>
        {
            new(ActionId: 1, DamageReduction: 0.2f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 30f, Tier: MitigationTier.Small),
        };

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, active);

        Assert.Null(result);
    }

    [Fact]
    public void SelectMinimumMitigation_ShieldAndHpBonusReduceIncomingBudget()
    {
        // A large active shield should absorb the spike entirely, leaving nothing to mitigate.
        var request = Request(currentHp: 100_000, maxHp: 100_000, incomingDps: 0f,
            mechanicSpikeFraction: MitigationCoverageCalculator.RaidwideSpikeFraction);
        var active = new ActiveMitigationState(CombinedDamageReduction: 0f, ActiveShield: 100_000f, ActiveMaxHpBonusFraction: 0f, InvulnActive: false);
        var options = new List<MitigationOption>
        {
            new(ActionId: 1, DamageReduction: 0.2f, ShieldPotency: 0f, MaxHpBonusFraction: 0f, CooldownSeconds: 30f, Tier: MitigationTier.Small),
        };

        var result = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, active);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(0f, 0.5f, 0.5f)]
    [InlineData(0.5f, 0f, 0.5f)]
    [InlineData(0.5f, 0.5f, 0.75f)]
    [InlineData(1f, 0.5f, 1f)]
    public void CombineReduction_MultiplicativeStacking(float existing, float additional, float expected)
    {
        var result = MitigationCoverageCalculator.CombineReduction(existing, additional);

        Assert.Equal(expected, result, precision: 5);
    }
}
