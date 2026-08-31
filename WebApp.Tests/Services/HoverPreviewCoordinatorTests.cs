using Microsoft.Extensions.Options;
using WebApp.Client.Models;
using WebApp.Configuration;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class HoverPreviewCoordinatorTests
{
    [Fact]
    public void Resolve_returns_unavailable_for_a_missing_entry()
    {
        var coordinator = CreateCoordinator(out _, capacity: 4);

        Assert.Equal(HoverPreviewState.Unavailable, coordinator.Resolve(null));
    }

    [Fact]
    public void Resolve_returns_pending_when_thumbnail_is_ready_but_no_preview_file_exists()
    {
        var coordinator = CreateCoordinator(out var previewPath, capacity: 4);
        var entry = CreateEntry("clip.mp4");
        WriteReadyThumbnail(previewPath, entry);

        Assert.Equal(HoverPreviewState.Pending, coordinator.Resolve(entry));
    }

    [Fact]
    public void Resolve_returns_pending_even_when_thumbnail_is_not_ready()
    {
        var coordinator = CreateCoordinator(out _, capacity: 4);
        var entry = CreateEntry("clip.mp4");

        Assert.Equal(HoverPreviewState.Pending, coordinator.Resolve(entry));
    }

    [Fact]
    public void Resolve_returns_ready_when_a_valid_final_file_exists()
    {
        var coordinator = CreateCoordinator(out var previewPath, capacity: 4);
        var entry = CreateEntry("clip.mp4");
        WriteReadyPreview(previewPath, entry);

        Assert.Equal(HoverPreviewState.Ready, coordinator.Resolve(entry));
    }

    [Fact]
    public void Resolve_returns_failed_after_marking_a_key_failed()
    {
        var coordinator = CreateCoordinator(out _, capacity: 4);
        var entry = CreateEntry("clip.mp4");

        coordinator.MarkFailed(coordinator.ComputeKey(entry));

        Assert.Equal(HoverPreviewState.Failed, coordinator.Resolve(entry));
    }

    [Fact]
    public void A_changed_source_computes_a_different_key_and_is_not_suppressed()
    {
        var coordinator = CreateCoordinator(out _, capacity: 4);
        var original = CreateEntry("clip.mp4");
        coordinator.MarkFailed(coordinator.ComputeKey(original));

        var changed = original with { SizeBytes = original.SizeBytes + 1 };

        Assert.Equal(HoverPreviewState.Pending, coordinator.Resolve(changed));
    }

    [Fact]
    public void A_fresh_coordinator_after_restart_permits_a_new_attempt()
    {
        var entry = CreateEntry("clip.mp4");
        var firstCoordinator = CreateCoordinator(out var previewPath, capacity: 4);
        firstCoordinator.MarkFailed(firstCoordinator.ComputeKey(entry));
        Assert.Equal(HoverPreviewState.Failed, firstCoordinator.Resolve(entry));

        var restarted = CreateCoordinatorAt(previewPath, capacity: 4);

        Assert.Equal(HoverPreviewState.Pending, restarted.Resolve(entry));
    }

    [Fact]
    public void Reconcile_does_not_enqueue_when_the_thumbnail_is_not_ready()
    {
        var coordinator = CreateCoordinator(out _, capacity: 4, out var queue);
        var entry = CreateEntry("clip.mp4");

        coordinator.Reconcile([entry]);

        Assert.False(queue.IsActive(coordinator.ComputeKey(entry)));
    }

    [Fact]
    public void Reconcile_enqueues_missing_entries_and_skips_ready_or_failed_ones_once_thumbnails_are_ready()
    {
        var coordinator = CreateCoordinator(out var previewPath, capacity: 4, out var queue);
        var ready = CreateEntry("ready.mp4");
        var failed = CreateEntry("failed.mp4");
        var pending = CreateEntry("pending.mp4");
        WriteReadyThumbnail(previewPath, ready);
        WriteReadyThumbnail(previewPath, failed);
        WriteReadyThumbnail(previewPath, pending);
        WriteReadyPreview(previewPath, ready);
        coordinator.MarkFailed(coordinator.ComputeKey(failed));

        coordinator.Reconcile([ready, failed, pending]);

        Assert.True(queue.IsActive(coordinator.ComputeKey(pending)));
        Assert.False(queue.IsActive(coordinator.ComputeKey(ready)));
        Assert.False(queue.IsActive(coordinator.ComputeKey(failed)));
    }

    [Fact]
    public void Disabled_options_prevent_enqueue_and_resolve_to_unavailable()
    {
        var coordinator = CreateCoordinator(out var previewPath, capacity: 4, out var queue, enabled: false);
        var entry = CreateEntry("clip.mp4");
        WriteReadyThumbnail(previewPath, entry);

        coordinator.Reconcile([entry]);

        Assert.False(queue.IsActive(coordinator.ComputeKey(entry)));
        Assert.Equal(HoverPreviewState.Unavailable, coordinator.Resolve(entry));
    }

    [Fact]
    public async Task Reconcile_admits_more_entries_than_capacity_across_worker_completions()
    {
        var coordinator = CreateCoordinator(out var previewPath, capacity: 1, out var queue);
        var first = CreateEntry("first.mp4");
        var second = CreateEntry("second.mp4");
        WriteReadyThumbnail(previewPath, first);
        WriteReadyThumbnail(previewPath, second);

        coordinator.Reconcile([first, second]);
        Assert.True(queue.IsActive(coordinator.ComputeKey(first)));
        Assert.False(queue.IsActive(coordinator.ComputeKey(second)));

        var dequeued = await queue.DequeueAsync(CancellationToken.None);
        Assert.Equal(coordinator.ComputeKey(first), dequeued.CacheKey);
        WriteReadyPreview(previewPath, first);
        queue.Release(dequeued.CacheKey);

        coordinator.Reconcile([first, second]);
        Assert.False(queue.IsActive(coordinator.ComputeKey(first)));
        Assert.True(queue.IsActive(coordinator.ComputeKey(second)));
    }

    private static void WriteReadyThumbnail(string previewPath, VideoFileEntry entry)
    {
        var cache = new ThumbnailCache(Options.Create(new ThumbnailCacheOptions { Path = previewPath }));
        var key = cache.ComputeKey(entry.RelativePath, entry.SizeBytes, entry.LastWriteTimeUtc);
        File.WriteAllBytes(cache.GetFinalPath(key), [1, 2, 3]);
    }

    private static void WriteReadyPreview(string previewPath, VideoFileEntry entry)
    {
        var cache = new HoverPreviewCache(Options.Create(new ThumbnailCacheOptions { Path = previewPath }));
        var key = cache.ComputeKey(entry.RelativePath, entry.SizeBytes, entry.LastWriteTimeUtc);
        File.WriteAllBytes(cache.GetFinalPath(key), [1, 2, 3]);
    }

    private static VideoFileEntry CreateEntry(string relativePath) => new(
        Guid.NewGuid().ToString("N"),
        $"/videos/{relativePath}",
        relativePath,
        relativePath,
        Path.GetExtension(relativePath),
        100,
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    private static HoverPreviewCoordinator CreateCoordinator(out string previewPath, int capacity) =>
        CreateCoordinator(out previewPath, capacity, out _);

    private static HoverPreviewCoordinator CreateCoordinator(
        out string previewPath, int capacity, out HoverPreviewJobQueue queue, bool enabled = true)
    {
        previewPath = Path.Combine(Path.GetTempPath(), $"video-manager-tests-preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(previewPath);
        var thumbnailOptions = Options.Create(new ThumbnailCacheOptions { Path = previewPath, QueueCapacity = 64 });
        var thumbnailCoordinator = new ThumbnailCoordinator(new ThumbnailCache(thumbnailOptions), new ThumbnailJobQueue(thumbnailOptions));
        var hoverOptions = Options.Create(new HoverPreviewOptions { QueueCapacity = capacity, Enabled = enabled });
        queue = new HoverPreviewJobQueue(hoverOptions);
        return new HoverPreviewCoordinator(
            new HoverPreviewCache(thumbnailOptions), queue, thumbnailCoordinator, hoverOptions);
    }

    private static HoverPreviewCoordinator CreateCoordinatorAt(string previewPath, int capacity)
    {
        var thumbnailOptions = Options.Create(new ThumbnailCacheOptions { Path = previewPath, QueueCapacity = 64 });
        var thumbnailCoordinator = new ThumbnailCoordinator(new ThumbnailCache(thumbnailOptions), new ThumbnailJobQueue(thumbnailOptions));
        var hoverOptions = Options.Create(new HoverPreviewOptions { QueueCapacity = capacity });
        return new HoverPreviewCoordinator(
            new HoverPreviewCache(thumbnailOptions), new HoverPreviewJobQueue(hoverOptions), thumbnailCoordinator, hoverOptions);
    }
}
