using WrathCombo.CustomComboNS.Functions;

namespace WrathCombo.Tests;

public class GcdCycleTrackerTests
{
    [Fact]
    public void Update_ReportsEndOfGcdWithoutWaitingForAnotherAction()
    {
        var tracker = new GcdCycleTracker();

        Assert.True(tracker.TryUpdate(2.5f, out var started));
        Assert.True(started);
        Assert.False(tracker.TryUpdate(1.0f, out _));

        Assert.True(tracker.TryUpdate(0.05f, out var ended));
        Assert.False(ended);

        Assert.True(tracker.TryUpdate(0.1f, out var restarted));
        Assert.True(restarted);
    }
}
