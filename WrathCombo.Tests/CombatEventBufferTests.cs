using System;
using WrathCombo.Services.SmartMitigation;
using Xunit;

namespace WrathCombo.Tests;

// PruneOldEvents is intentionally not covered here: it is coupled to DateTime.UtcNow (wall clock),
// and a sleep-based test would be flaky. Aggregation methods below take caller-supplied timestamps,
// so they are fully testable without touching real time.
public class CombatEventBufferTests
{
    private static readonly DateTime AnyTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetTotalDamage_SumsOnlyDamageEventsForTarget()
    {
        var buffer = new CombatEventBuffer(windowSeconds: 10f);
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 1, Amount: 500f, CombatEventType.Damage, AnyTimestamp));
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 2, Amount: 300f, CombatEventType.Damage, AnyTimestamp));
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 3, Amount: 200f, CombatEventType.Healing, AnyTimestamp));
        buffer.AddEvent(new CombatEvent(TargetId: 2, SourceId: 100, ActionId: 4, Amount: 999f, CombatEventType.Damage, AnyTimestamp));

        Assert.Equal(800f, buffer.GetTotalDamage(targetId: 1));
    }

    [Fact]
    public void GetTotalHealing_SumsOnlyHealingEventsForTarget()
    {
        var buffer = new CombatEventBuffer(windowSeconds: 10f);
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 1, Amount: 400f, CombatEventType.Healing, AnyTimestamp));
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 2, Amount: 600f, CombatEventType.Healing, AnyTimestamp));
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 3, Amount: 100f, CombatEventType.Damage, AnyTimestamp));

        Assert.Equal(1000f, buffer.GetTotalHealing(targetId: 1));
    }

    [Fact]
    public void GetMaxSingleHit_ReturnsLargestDamageEventForTarget()
    {
        var buffer = new CombatEventBuffer(windowSeconds: 10f);
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 1, Amount: 200f, CombatEventType.Damage, AnyTimestamp));
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 2, Amount: 900f, CombatEventType.Damage, AnyTimestamp));
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 3, Amount: 500f, CombatEventType.Damage, AnyTimestamp));
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 4, Amount: 5000f, CombatEventType.Healing, AnyTimestamp));

        Assert.Equal(900f, buffer.GetMaxSingleHit(targetId: 1));
    }

    [Fact]
    public void GetMaxSingleHit_NoDamageEvents_ReturnsZero()
    {
        var buffer = new CombatEventBuffer(windowSeconds: 10f);
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 1, Amount: 300f, CombatEventType.Healing, AnyTimestamp));

        Assert.Equal(0f, buffer.GetMaxSingleHit(targetId: 1));
    }

    [Fact]
    public void Clear_RemovesAllEvents()
    {
        var buffer = new CombatEventBuffer(windowSeconds: 10f);
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 1, Amount: 500f, CombatEventType.Damage, AnyTimestamp));

        buffer.Clear();

        Assert.Equal(0f, buffer.GetTotalDamage(targetId: 1));
        Assert.Equal(0f, buffer.GetTotalHealing(targetId: 1));
        Assert.Equal(0f, buffer.GetMaxSingleHit(targetId: 1));
    }

    [Fact]
    public void GetTotalDamage_UnknownTarget_ReturnsZero()
    {
        var buffer = new CombatEventBuffer(windowSeconds: 10f);
        buffer.AddEvent(new CombatEvent(TargetId: 1, SourceId: 100, ActionId: 1, Amount: 500f, CombatEventType.Damage, AnyTimestamp));

        Assert.Equal(0f, buffer.GetTotalDamage(targetId: 999));
    }
}
