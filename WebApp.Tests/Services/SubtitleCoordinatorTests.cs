using Microsoft.Extensions.Options;
using WebApp.Client.Models;
using WebApp.Configuration;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class SubtitleCoordinatorTests
{
    [Fact]
    public async Task Resolve_reports_unavailable_pending_ready_and_failed()
    {
        using var media = new TemporaryDirectory();
        using var preview = new TemporaryDirectory();
        var entry = await CreateMediaPairAsync(media.Path, "clip.mp4", withSubtitle: false);
        var coordinator = CreateCoordinator(preview.Path, out _);

        Assert.Equal(SubtitleState.Unavailable, coordinator.Resolve(entry));

        await File.WriteAllTextAsync(Path.ChangeExtension(entry.PhysicalPath, ".srt"), "subtitle");
        var withSubtitle = Refresh(entry);
        Assert.Equal(SubtitleState.Pending, coordinator.Resolve(withSubtitle));

        var key = coordinator.ComputeKey(withSubtitle)!;
        await File.WriteAllTextAsync(new SubtitleCache(Options.Create(new ThumbnailCacheOptions { Path = preview.Path })).GetFinalPath(key), "WEBVTT");
        Assert.Equal(SubtitleState.Ready, coordinator.Resolve(withSubtitle));

        var failed = await CreateMediaPairAsync(media.Path, "failed.mp4", withSubtitle: true);
        coordinator.MarkFailed(coordinator.ComputeKey(failed)!);
        Assert.Equal(SubtitleState.Failed, coordinator.Resolve(failed));
    }

    [Fact]
    public async Task Reconcile_enqueues_only_matched_pending_entries_from_library_and_archive_sources()
    {
        using var media = new TemporaryDirectory();
        using var preview = new TemporaryDirectory();
        var library = await CreateMediaPairAsync(media.Path, "library.mp4", withSubtitle: true);
        var archive = (await CreateMediaPairAsync(media.Path, "archive.mp4", withSubtitle: true)) with
        {
            RelativePath = "archive/downloads/id/archive.mp4"
        };
        var unmatched = await CreateMediaPairAsync(media.Path, "plain.mp4", withSubtitle: false);
        var coordinator = CreateCoordinator(preview.Path, out var queue);

        coordinator.Reconcile([library, archive, unmatched]);

        Assert.True(queue.IsActive(coordinator.ComputeKey(library)!));
        Assert.True(queue.IsActive(coordinator.ComputeKey(archive)!));
        Assert.Null(coordinator.ComputeKey(unmatched));

        var readyKey = coordinator.ComputeKey(library)!;
        await File.WriteAllTextAsync(new SubtitleCache(Options.Create(new ThumbnailCacheOptions { Path = preview.Path })).GetFinalPath(readyKey), "WEBVTT");
        queue.Release(readyKey);
        var failedKey = coordinator.ComputeKey(archive)!;
        queue.Release(failedKey);
        coordinator.MarkFailed(failedKey);

        coordinator.Reconcile([library, archive]);

        Assert.False(queue.IsActive(readyKey));
        Assert.False(queue.IsActive(failedKey));
    }

    private static SubtitleCoordinator CreateCoordinator(string previewPath, out SubtitleJobQueue queue)
    {
        var options = Options.Create(new ThumbnailCacheOptions { Path = previewPath, QueueCapacity = 8 });
        queue = new SubtitleJobQueue(options);
        return new SubtitleCoordinator(new SubtitleCache(options), new SubtitleMatcher(), queue);
    }

    private static async Task<VideoFileEntry> CreateMediaPairAsync(string directory, string name, bool withSubtitle)
    {
        var videoPath = Path.Combine(directory, name);
        await File.WriteAllBytesAsync(videoPath, [1, 2, 3]);
        if (withSubtitle)
        {
            await File.WriteAllTextAsync(Path.ChangeExtension(videoPath, ".srt"), "subtitle");
        }

        return Refresh(new VideoFileEntry(
            Guid.NewGuid().ToString("N"),
            videoPath,
            name,
            name,
            Path.GetExtension(name),
            0,
            DateTime.MinValue));
    }

    private static VideoFileEntry Refresh(VideoFileEntry entry)
    {
        var file = new FileInfo(entry.PhysicalPath);
        return entry with { SizeBytes = file.Length, LastWriteTimeUtc = file.LastWriteTimeUtc };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-subtitle-coord-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
