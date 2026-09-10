using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class SubtitleCacheTests
{
    private static readonly DateTime SampleTime = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Identical_identity_inputs_produce_the_same_key()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);
        var entry = CreateEntry("clip.mp4");
        var subtitle = CreateSubtitle();

        var first = cache.ComputeKey(entry, subtitle);
        var second = cache.ComputeKey(entry, subtitle);

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    public void Video_or_subtitle_identity_changes_change_the_key()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);
        var entry = CreateEntry("clip.mp4");
        var subtitle = CreateSubtitle();

        var baseline = cache.ComputeKey(entry, subtitle);

        Assert.NotEqual(baseline, cache.ComputeKey(entry with { SizeBytes = entry.SizeBytes + 1 }, subtitle));
        Assert.NotEqual(baseline, cache.ComputeKey(entry with { LastWriteTimeUtc = entry.LastWriteTimeUtc.AddSeconds(1) }, subtitle));
        Assert.NotEqual(baseline, cache.ComputeKey(entry, subtitle with { SizeBytes = subtitle.SizeBytes + 1 }));
        Assert.NotEqual(baseline, cache.ComputeKey(entry, subtitle with { LastWriteTimeUtc = subtitle.LastWriteTimeUtc.AddSeconds(1) }));
    }

    [Fact]
    public void Same_named_library_and_archive_entries_do_not_collide()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);
        var subtitle = CreateSubtitle();

        var library = CreateEntry("movie.mp4");
        var archive = library with { RelativePath = "archive/downloads/id/movie.mp4" };

        Assert.NotEqual(cache.ComputeKey(library, subtitle), cache.ComputeKey(archive, subtitle));
    }

    [Fact]
    public void Paths_resolve_under_subtitles_subdirectory()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);
        var key = cache.ComputeKey(CreateEntry("clip.mp4"), CreateSubtitle());

        var finalPath = cache.GetFinalPath(key);
        var temporaryPath = cache.GetTemporaryPath(key);

        var subtitleRoot = Path.Combine(Path.GetFullPath(preview.Path), "subtitles");
        Assert.StartsWith(subtitleRoot, finalPath);
        Assert.StartsWith(subtitleRoot, temporaryPath);
        Assert.EndsWith($"{key}.vtt", finalPath);
        Assert.Contains(".tmp.vtt", temporaryPath);
    }

    [Fact]
    public void Only_a_non_empty_final_file_is_ready()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);
        var key = cache.ComputeKey(CreateEntry("clip.mp4"), CreateSubtitle());

        Assert.False(cache.IsReady(key));
        File.WriteAllBytes(cache.GetFinalPath(key), []);
        Assert.False(cache.IsReady(key));
        File.WriteAllBytes(cache.GetFinalPath(key), [1]);
        Assert.True(cache.IsReady(key));
    }

    private static SubtitleCache CreateCache(string previewPath) =>
        new(Options.Create(new ThumbnailCacheOptions { Path = previewPath }));

    private static VideoFileEntry CreateEntry(string relativePath) => new(
        Guid.NewGuid().ToString("N"),
        $"/videos/{relativePath}",
        relativePath,
        relativePath,
        Path.GetExtension(relativePath),
        100,
        SampleTime);

    private static SubtitleFileInfo CreateSubtitle() =>
        new("/videos/clip.srt", 20, SampleTime.AddMinutes(1));

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-subtitle-cache-{Guid.NewGuid():N}");
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
