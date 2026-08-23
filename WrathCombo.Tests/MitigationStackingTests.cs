using WrathCombo.Services.SmartMitigation;
using Xunit;

namespace WrathCombo.Tests;

public class MitigationStackingTests
{
    private static ActiveMitigationState State(
        bool longActive = false,
        bool shortActive = false,
        bool invuln = false) =>
        new(0f, 0f, 0f, invuln, longActive, shortActive);

    [Fact]
    public void LongActive_ExcludesLongOption()
    {
        var result = TrashMitigationOrdering.ShouldExcludeForStacking(
            MitigationPool.Long, State(longActive: true), isBoss: true);

        Assert.True(result);
    }

    [Fact]
    public void LongActive_AllowsShortOption()
    {
        var result = TrashMitigationOrdering.ShouldExcludeForStacking(
            MitigationPool.Short, State(longActive: true), isBoss: true);

        Assert.False(result);
    }

    [Fact]
    public void ShortActive_ExcludesShortOption()
    {
        var result = TrashMitigationOrdering.ShouldExcludeForStacking(
            MitigationPool.Short, State(shortActive: true), isBoss: true);

        Assert.True(result);
    }

    [Fact]
    public void ShortActive_AllowsLongOption()
    {
        var result = TrashMitigationOrdering.ShouldExcludeForStacking(
            MitigationPool.Long, State(shortActive: true), isBoss: true);

        Assert.False(result);
    }

    [Fact]
    public void BothActive_ExcludesBothPools()
    {
        var active = State(longActive: true, shortActive: true);

        Assert.True(TrashMitigationOrdering.ShouldExcludeForStacking(MitigationPool.Long, active, isBoss: true));
        Assert.True(TrashMitigationOrdering.ShouldExcludeForStacking(MitigationPool.Short, active, isBoss: true));
    }

    [Fact]
    public void BothActive_AllowsExempt()
    {
        var active = State(longActive: true, shortActive: true);

        Assert.False(TrashMitigationOrdering.ShouldExcludeForStacking(MitigationPool.Exempt, active, isBoss: true));
    }

    [Fact]
    public void Exempt_NeverExcluded()
    {
        Assert.False(TrashMitigationOrdering.ShouldExcludeForStacking(MitigationPool.Exempt, State(), isBoss: true));
        Assert.False(TrashMitigationOrdering.ShouldExcludeForStacking(MitigationPool.Exempt, State(longActive: true, shortActive: true), isBoss: false));
    }

    [Fact]
    public void TrashOnly_ExcludedOnBoss()
    {
        var result = TrashMitigationOrdering.ShouldExcludeForStacking(
            MitigationPool.TrashOnly, State(), isBoss: true);

        Assert.True(result);
    }

    [Fact]
    public void TrashOnly_AllowedOnTrash()
    {
        var result = TrashMitigationOrdering.ShouldExcludeForStacking(
            MitigationPool.TrashOnly, State(), isBoss: false);

        Assert.False(result);
    }

    [Fact]
    public void NothingActive_AllowsAllPools()
    {
        var active = State();

        Assert.False(TrashMitigationOrdering.ShouldExcludeForStacking(MitigationPool.Long, active, isBoss: true));
        Assert.False(TrashMitigationOrdering.ShouldExcludeForStacking(MitigationPool.Short, active, isBoss: true));
        Assert.False(TrashMitigationOrdering.ShouldExcludeForStacking(MitigationPool.Exempt, active, isBoss: true));
    }

    [Fact]
    public void ClassifyPool_Exactly60s_IsShort() =>
        Assert.Equal(MitigationPool.Short, TrashMitigationOrdering.ClassifyPool(60f));

    [Fact]
    public void ClassifyPool_61s_IsLong() =>
        Assert.Equal(MitigationPool.Long, TrashMitigationOrdering.ClassifyPool(61f));

    [Fact]
    public void ClassifyPool_90s_IsLong() =>
        Assert.Equal(MitigationPool.Long, TrashMitigationOrdering.ClassifyPool(90f));

    [Fact]
    public void ClassifyPool_120s_IsLong() =>
        Assert.Equal(MitigationPool.Long, TrashMitigationOrdering.ClassifyPool(120f));

    [Fact]
    public void ClassifyPool_25s_IsShort() =>
        Assert.Equal(MitigationPool.Short, TrashMitigationOrdering.ClassifyPool(25f));

    [Fact]
    public void ClassifyPool_5s_IsShort() =>
        Assert.Equal(MitigationPool.Short, TrashMitigationOrdering.ClassifyPool(5f));

    [Fact]
    public void ClassifyPool_ExemptFlag_OverridesLongCooldown() =>
        Assert.Equal(MitigationPool.Exempt, TrashMitigationOrdering.ClassifyPool(90f, isExemptAction: true));

    [Fact]
    public void ClassifyPool_TrashOnlyFlag_OverridesLongCooldown() =>
        Assert.Equal(MitigationPool.TrashOnly, TrashMitigationOrdering.ClassifyPool(120f, isTrashOnly: true));

    [Fact]
    public void ClassifyPool_ExemptTakesPriorityOverTrashOnly() =>
        Assert.Equal(MitigationPool.Exempt, TrashMitigationOrdering.ClassifyPool(90f, isExemptAction: true, isTrashOnly: true));

    [Fact]
    public void Selection_LongActive_SkipsLongOptions_PicksShort()
    {
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(100, 0.30f, 0f, 0f, 120f, MitigationTier.Large, MitigationPool.Long),
            new(200, 0.20f, 0f, 0f, 60f, MitigationTier.Medium, MitigationPool.Short),
        };

        var active = State(longActive: true);
        uint actionId = 0;

        var result = TankMitigationSelection.TryPickLowestTier(
            options, active, isBoss: true, _ => true, ref actionId);

        Assert.True(result);
        Assert.Equal(200u, actionId);
    }

    [Fact]
    public void Selection_ShortActive_SkipsShortOptions_PicksLong()
    {
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(100, 0.30f, 0f, 0f, 120f, MitigationTier.Large, MitigationPool.Long),
            new(200, 0.20f, 0f, 0f, 60f, MitigationTier.Medium, MitigationPool.Short),
        };

        var active = State(shortActive: true);
        uint actionId = 0;

        var result = TankMitigationSelection.TryPickLowestTier(
            options, active, isBoss: true, _ => true, ref actionId);

        Assert.True(result);
        Assert.Equal(100u, actionId);
    }

    [Fact]
    public void Selection_BothActive_OnlyExemptRemains()
    {
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(100, 0.30f, 0f, 0f, 120f, MitigationTier.Large, MitigationPool.Long),
            new(200, 0.20f, 0f, 0f, 60f, MitigationTier.Medium, MitigationPool.Short),
            new(300, 0.10f, 0f, 0f, 25f, MitigationTier.Small, MitigationPool.Exempt),
        };

        var active = State(longActive: true, shortActive: true);
        uint actionId = 0;

        var result = TankMitigationSelection.TryPickLowestTier(
            options, active, isBoss: true, _ => true, ref actionId);

        Assert.True(result);
        Assert.Equal(300u, actionId);
    }

    [Fact]
    public void Selection_TrashOnly_ExcludedOnBoss()
    {
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(400, 0.20f, 0f, 0f, 120f, MitigationTier.Medium, MitigationPool.TrashOnly),
        };

        var active = State();
        uint actionId = 0;

        var result = TankMitigationSelection.TryPickLowestTier(
            options, active, isBoss: true, _ => true, ref actionId);

        Assert.False(result);
    }

    [Fact]
    public void Selection_TrashOnly_AllowedOnTrash()
    {
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(400, 0.20f, 0f, 0f, 120f, MitigationTier.Medium, MitigationPool.TrashOnly),
        };

        var active = State();
        uint actionId = 0;

        var result = TankMitigationSelection.TryPickLowestTier(
            options, active, isBoss: false, _ => true, ref actionId);

        Assert.True(result);
        Assert.Equal(400u, actionId);
    }

    [Fact]
    public void DrkTrashFallback_HighHpWithoutReprisal_SelectsEligiblePersonalMitigation()
    {
        // Reprisal is omitted/unavailable; coverage declines this modest trash hit, then the shared
        // TCH danger fallback must still select DRK personal mitigation at 95% HP.
        var request = new MitigationCoverageRequest(
            CurrentHp: 95_000,
            MaxHp: 100_000,
            IncomingDps: 0f,
            IncomingHps: 0f,
            MechanicSpikeFraction: MitigationCoverageCalculator.RaidwideSpikeFraction,
            HorizonSeconds: MitigationCoverageCalculator.TrashHorizonSeconds,
            SafetyHpPercent: MitigationCoverageCalculator.TrashSafetyHpPercent,
            ConfirmedTankbuster: false,
            SustainMultiplier: 1f,
            PreferHeavyMitigation: false);
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(3634, 0.20f, 0f, 0f, 60f, MitigationTier.Medium, MitigationPool.Short),
        };

        var active = State();
        uint actionId = 0;
        var coverage = MitigationCoverageCalculator.SelectMinimumMitigation(request, options, active, isBoss: false);

        Assert.Equal(95f, (float)request.CurrentHp / request.MaxHp * 100f);
        Assert.Null(coverage);

        var result = TankMitigationSelection.TryPickLowestTier(
            options, active, isBoss: false, _ => true, ref actionId);

        Assert.True(result);
        Assert.Equal(3634u, actionId);
    }

    [Fact]
    public void SustainedSoftBossPressure_NoActiveMitigation_PicksNonHeavyFallback()
    {
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(3636, 0.30f, 0f, 0f, 120f, MitigationTier.Large, MitigationPool.Long),
            new(7531, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Long),
            new(3634, 0.20f, 0f, 0f, 60f, MitigationTier.Medium, MitigationPool.Short),
        };
        uint actionId = 0;

        var result = TankMitigationSelection.TryPickSustainedSoftTankbusterFallback(
            options,
            State(),
            isBoss: true,
            softTankbuster: true,
            sustainedPressure: true,
            fromTankCooldownHelper: false,
            _ => true,
            ref actionId);

        Assert.True(result);
        Assert.Equal(7531u, actionId);
    }

    [Fact]
    public void SustainedSoftBossPressure_ActivePool_DoesNotStackFallback()
    {
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(7531, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Long),
            new(3634, 0.20f, 0f, 0f, 60f, MitigationTier.Medium, MitigationPool.Short),
        };
        uint actionId = 0;

        var result = TankMitigationSelection.TryPickSustainedSoftTankbusterFallback(
            options,
            State(longActive: true),
            isBoss: true,
            softTankbuster: true,
            sustainedPressure: true,
            fromTankCooldownHelper: false,
            _ => true,
            ref actionId);

        Assert.False(result);
        Assert.Equal(0u, actionId);
    }

    [Fact]
    public void SoftBossTargeting_WithoutSustainedPressure_DoesNotFallback()
    {
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(7531, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Long),
        };
        uint actionId = 0;

        var result = TankMitigationSelection.TryPickSustainedSoftTankbusterFallback(
            options,
            State(),
            isBoss: true,
            softTankbuster: true,
            sustainedPressure: false,
            fromTankCooldownHelper: false,
            _ => true,
            ref actionId);

        Assert.False(result);
        Assert.Equal(0u, actionId);
    }

    [Theory]
    [InlineData(false, true, true, false, false, false)]
    [InlineData(true, false, true, false, false, false)]
    [InlineData(true, true, true, true, false, false)]
    [InlineData(true, true, true, false, true, false)]
    [InlineData(true, true, true, false, false, true)]
    public void SustainedSoftBossPressure_UnsafeContext_DoesNotFallback(
        bool isBoss,
        bool softTankbuster,
        bool sustainedPressure,
        bool shortActive,
        bool invuln,
        bool fromTankCooldownHelper)
    {
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(7531, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Long),
        };
        uint actionId = 0;

        var result = TankMitigationSelection.TryPickSustainedSoftTankbusterFallback(
            options,
            State(shortActive: shortActive, invuln: invuln),
            isBoss,
            softTankbuster,
            sustainedPressure,
            fromTankCooldownHelper,
            _ => true,
            ref actionId);

        Assert.False(result);
        Assert.Equal(0u, actionId);
    }

    [Fact]
    public void GnbSustainedSoftBossFallback_PicksRampartBeforeCamouflage()
    {
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(36935, 0.40f, 0f, 0f, 120f, MitigationTier.Large, MitigationPool.Long),
            new(7531, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Long),
            new(16140, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Long),
        };
        uint actionId = 0;

        var result = TankMitigationSelection.TryPickTankMitigationFallback(
            options,
            State(),
            isBoss: true,
            strictTankbuster: false,
            softTankbuster: true,
            sustainedPressure: true,
            fromTankCooldownHelper: false,
            _ => true,
            ref actionId);

        Assert.True(result);
        Assert.Equal(7531u, actionId);
    }

    [Fact]
    public void GnbStrictTankbusterFallback_RemainsAvailableForTchPressure()
    {
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(7531, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Long),
        };
        uint actionId = 0;

        var result = TankMitigationSelection.TryPickTankMitigationFallback(
            options,
            State(),
            isBoss: true,
            strictTankbuster: true,
            softTankbuster: false,
            sustainedPressure: false,
            fromTankCooldownHelper: true,
            _ => true,
            ref actionId);

        Assert.True(result);
        Assert.Equal(7531u, actionId);
    }

    [Fact]
    public void GnbTelemetryFallback_TchOwnedPressure_DoesNotCompete()
    {
        var options = new System.Collections.Generic.List<MitigationOption>
        {
            new(7531, 0.20f, 0f, 0f, 90f, MitigationTier.Medium, MitigationPool.Long),
        };
        uint actionId = 0;

        var result = TankMitigationSelection.TryPickTankMitigationFallback(
            options,
            State(),
            isBoss: true,
            strictTankbuster: false,
            softTankbuster: true,
            sustainedPressure: true,
            fromTankCooldownHelper: true,
            _ => true,
            ref actionId);

        Assert.False(result);
        Assert.Equal(0u, actionId);
    }
}
