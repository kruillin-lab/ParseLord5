using System.Collections.Generic;

namespace WrathCombo.Services.SmartMitigation;

internal sealed class SmartMitigationTraceThrottle(long intervalMilliseconds)
{
    private readonly Dictionary<string, long> _nextTraceAtBySource = new();

    public bool ShouldLog(long now, string source)
    {
        if (_nextTraceAtBySource.TryGetValue(source, out var nextTraceAt) && now < nextTraceAt)
            return false;

        _nextTraceAtBySource[source] = now + intervalMilliseconds;
        return true;
    }
}
