using WrathCombo.Services.SmartMitigation;

namespace WrathCombo.Tests;

public class SmartMitigationTraceThrottleTests
{
    [Fact]
    public void DifferentDecisionSources_DoNotSuppressEachOther()
    {
        var throttle = new SmartMitigationTraceThrottle(intervalMilliseconds: 5_000);

        Assert.True(throttle.ShouldLog(1_000, "enter"));
        Assert.True(throttle.ShouldLog(1_000, "no_threat"));

        Assert.False(throttle.ShouldLog(5_999, "enter"));
        Assert.False(throttle.ShouldLog(5_999, "no_threat"));

        Assert.True(throttle.ShouldLog(6_000, "enter"));
        Assert.True(throttle.ShouldLog(6_000, "no_threat"));
    }
}
