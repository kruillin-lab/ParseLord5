using WrathCombo.Services;
using Xunit;

namespace WrathCombo.Tests;

/// <summary>
///     Behavioral coverage for the Auto-Rotation peek resolution and DPS-lane
///     gate. The SGE redirect regression this locks: an ActionStacksEX stack
///     turning Dosis into Kerachole used to skip CanUseAutorotDpsAction entirely.
/// </summary>
public class AutorotActionPolicyTests
{
    private const uint Dosis = 24283u;
    private const uint Eukrasia = 24290u;
    private const uint Kardia = 24285u;
    private const uint Kerachole = 24298u;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PeekKey_UsesGameActOnlyWhenActionChanging(bool actionChanging)
    {
        var key = AutorotActionPolicy.PeekKey(gameAct: 111u, outAct: 222u, actionChanging);

        Assert.Equal(actionChanging ? 111u : 222u, key);
    }

    [Fact]
    public void ResolveAction_RedirectReplacesTheChoice()
    {
        var resolved = AutorotActionPolicy.ResolveAction(Dosis, redirected: true, resolvedAction: Kerachole);

        Assert.Equal(Kerachole, resolved);
    }

    [Fact]
    public void ResolveAction_NoRedirectKeepsTheChoice()
    {
        var resolved = AutorotActionPolicy.ResolveAction(Dosis, redirected: false, resolvedAction: Kerachole);

        Assert.Equal(Dosis, resolved);
    }

    [Fact]
    public void DpsLane_NonSgeJobsAreNeverBlocked()
    {
        Assert.True(AutorotActionPolicy.AllowedInDpsLane(Kerachole, isSge: false));
        Assert.True(AutorotActionPolicy.AllowedInDpsLane(Kardia, isSge: false));
    }

    [Fact]
    public void DpsLane_SgeBlocklistRejectsEveryListedId()
    {
        Assert.Equal(19, AutorotActionPolicy.SgeBlockedInDpsLane.Count);

        foreach (var id in AutorotActionPolicy.SgeBlockedInDpsLane)
            Assert.False(AutorotActionPolicy.AllowedInDpsLane(id, isSge: true), $"id {id} must be blocked");
    }

    [Fact]
    public void DpsLane_SgeDpsActionsStayAllowed()
    {
        Assert.True(AutorotActionPolicy.AllowedInDpsLane(Dosis, isSge: true));
        Assert.True(AutorotActionPolicy.AllowedInDpsLane(Eukrasia, isSge: true));
    }

    [Fact]
    public void RedirectedDosisToKerachole_IsRejectedInSgeDpsLane()
    {
        var finalAction = AutorotActionPolicy.ResolveAction(Dosis, redirected: true, resolvedAction: Kerachole);

        Assert.False(AutorotActionPolicy.AllowedInDpsLane(finalAction, isSge: true));
    }
}
