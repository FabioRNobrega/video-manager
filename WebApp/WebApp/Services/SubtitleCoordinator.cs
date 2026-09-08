using System.Collections.Concurrent;
using WebApp.Client.Models;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class SubtitleCoordinator(
    SubtitleCache cache,
    SubtitleMatcher matcher,
    ISubtitleJobQueue queue)
{
    private readonly ConcurrentDictionary<string, bool> _failedKeys = new(StringComparer.Ordinal);

    public string? ComputeKey(VideoFileEntry entry)
    {
        var subtitle = matcher.FindMatch(entry);
        return subtitle is null ? null : cache.ComputeKey(entry, subtitle);
    }

    public string GetFinalPath(VideoFileEntry entry)
    {
        var subtitle = matcher.FindMatch(entry) ??
            throw new InvalidOperationException("No subtitle match is available for this media entry.");
        return cache.GetFinalPath(cache.ComputeKey(entry, subtitle));
    }

    public SubtitleState Resolve(VideoFileEntry? entry)
    {
        if (entry is null)
        {
            return SubtitleState.Unavailable;
        }

        var subtitle = matcher.FindMatch(entry);
        if (subtitle is null)
        {
            return SubtitleState.Unavailable;
        }

        var key = cache.ComputeKey(entry, subtitle);
        if (cache.IsReady(key))
        {
            return SubtitleState.Ready;
        }

        return _failedKeys.ContainsKey(key) ? SubtitleState.Failed : SubtitleState.Pending;
    }

    public void Reconcile(IReadOnlyList<VideoFileEntry> snapshot)
    {
        foreach (var entry in snapshot)
        {
            var subtitle = matcher.FindMatch(entry);
            if (subtitle is null)
            {
                continue;
            }

            var key = cache.ComputeKey(entry, subtitle);
            if (cache.IsReady(key) || _failedKeys.ContainsKey(key))
            {
                continue;
            }

            queue.TryEnqueue(new SubtitleJob(key, entry, subtitle));
        }
    }

    public void MarkFailed(string cacheKey) => _failedKeys[cacheKey] = true;
}
