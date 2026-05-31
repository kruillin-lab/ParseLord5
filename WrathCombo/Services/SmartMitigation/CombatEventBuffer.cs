using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WrathCombo.Services.SmartMitigation;

internal sealed class CombatEventBuffer
{
    private readonly ConcurrentQueue<CombatEvent> _events = new();
    private float _windowSeconds;

    public CombatEventBuffer(float windowSeconds) => _windowSeconds = windowSeconds;

    public void AddEvent(CombatEvent evt) => _events.Enqueue(evt);

    public void PruneOldEvents()
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-_windowSeconds);
        // Spin until head is within window — ConcurrentQueue has no Peek,
        // so we TryPeek and dequeue stale entries
        while (_events.TryPeek(out var evt) && evt.Timestamp < cutoff)
            _events.TryDequeue(out _);
    }

    public float GetTotalDamage(uint targetId)
    {
        var total = 0f;
        foreach (var e in _events)
            if (e.TargetId == targetId && e.Type == CombatEventType.Damage)
                total += e.Amount;
        return total;
    }

    public float GetTotalHealing(uint targetId)
    {
        var total = 0f;
        foreach (var e in _events)
            if (e.TargetId == targetId && e.Type == CombatEventType.Healing)
                total += e.Amount;
        return total;
    }

    public float GetMaxSingleHit(uint targetId)
    {
        var max = 0f;
        foreach (var e in _events)
            if (e.TargetId == targetId && e.Type == CombatEventType.Damage && e.Amount > max)
                max = e.Amount;
        return max;
    }

    public void Clear()
    {
        while (_events.TryDequeue(out _)) { }
    }
}

internal readonly record struct CombatEvent(
    uint TargetId,
    uint SourceId,
    uint ActionId,
    float Amount,
    CombatEventType Type,
    DateTime Timestamp);

internal enum CombatEventType
{
    Damage,
    Healing,
}
