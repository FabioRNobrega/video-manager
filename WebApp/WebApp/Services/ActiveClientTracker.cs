using System.Collections.Concurrent;

namespace WebApp.Services;

internal sealed class ActiveClientTracker : IActiveClientTracker
{
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, DateTime> _lastSeenUtc = new(StringComparer.Ordinal);

    public void Track(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return;
        }

        _lastSeenUtc[clientId] = DateTime.UtcNow;
    }

    public int CountActive()
    {
        var cutoff = DateTime.UtcNow - ActiveWindow;
        foreach (var entry in _lastSeenUtc)
        {
            if (entry.Value < cutoff)
            {
                _lastSeenUtc.TryRemove(entry.Key, out _);
            }
        }

        return _lastSeenUtc.Count;
    }
}
