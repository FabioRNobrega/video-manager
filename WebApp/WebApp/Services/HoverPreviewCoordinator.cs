using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using WebApp.Client.Models;
using WebApp.Configuration;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class HoverPreviewCoordinator(
    HoverPreviewCache cache,
    IHoverPreviewJobQueue queue,
    ThumbnailCoordinator thumbnailCoordinator,
    IOptions<HoverPreviewOptions> options)
{
    private readonly ConcurrentDictionary<string, bool> _failedKeys = new(StringComparer.Ordinal);

    public string ComputeKey(VideoFileEntry entry) =>
        cache.ComputeKey(entry.RelativePath, entry.SizeBytes, entry.LastWriteTimeUtc);

    public string GetFinalPath(VideoFileEntry entry) => cache.GetFinalPath(ComputeKey(entry));

    public HoverPreviewState Resolve(VideoFileEntry? entry)
    {
        if (entry is null || !options.Value.Enabled)
        {
            return HoverPreviewState.Unavailable;
        }

        var key = ComputeKey(entry);
        if (cache.IsReady(key))
        {
            return HoverPreviewState.Ready;
        }

        return _failedKeys.ContainsKey(key) ? HoverPreviewState.Failed : HoverPreviewState.Pending;
    }

    public void Reconcile(IReadOnlyList<VideoFileEntry> snapshot)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        foreach (var entry in snapshot)
        {
            if (thumbnailCoordinator.Resolve(entry) != ThumbnailState.Ready)
            {
                continue;
            }

            var key = ComputeKey(entry);
            if (cache.IsReady(key) || _failedKeys.ContainsKey(key))
            {
                continue;
            }

            queue.TryEnqueue(new HoverPreviewJob(key, entry));
        }
    }

    public void MarkFailed(string cacheKey) => _failedKeys[cacheKey] = true;
}
