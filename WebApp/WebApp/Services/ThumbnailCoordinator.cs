using System.Collections.Concurrent;
using WebApp.Client.Models;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class ThumbnailCoordinator(ThumbnailCache cache, IThumbnailJobQueue queue)
{
    private readonly ConcurrentDictionary<string, bool> _failedKeys = new(StringComparer.Ordinal);

    public string ComputeKey(VideoFileEntry entry) =>
        cache.ComputeKey(entry.RelativePath, entry.SizeBytes, entry.LastWriteTimeUtc);

    public string GetFinalPath(VideoFileEntry entry) => cache.GetFinalPath(ComputeKey(entry));

    public ThumbnailState Resolve(VideoFileEntry? entry)
    {
        if (entry is null)
        {
            return ThumbnailState.Unavailable;
        }

        var key = ComputeKey(entry);
        if (cache.IsReady(key))
        {
            return ThumbnailState.Ready;
        }

        return _failedKeys.ContainsKey(key) ? ThumbnailState.Failed : ThumbnailState.Pending;
    }

    public void Reconcile(IReadOnlyList<VideoFileEntry> snapshot)
    {
        foreach (var entry in snapshot)
        {
            var key = ComputeKey(entry);
            if (cache.IsReady(key) || _failedKeys.ContainsKey(key))
            {
                continue;
            }

            queue.TryEnqueue(new ThumbnailJob(key, entry));
        }
    }

    public void MarkFailed(string cacheKey) => _failedKeys[cacheKey] = true;
}
