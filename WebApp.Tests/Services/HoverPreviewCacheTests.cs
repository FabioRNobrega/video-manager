using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class HoverPreviewCacheTests
{
    private static readonly DateTime SampleTime = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Identical_identity_inputs_produce_the_same_key()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);

        var first = cache.ComputeKey("clip.mp4", 100, SampleTime);
        var second = cache.ComputeKey("clip.mp4", 100, SampleTime);

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    public void Changing_the_relative_path_changes_the_key()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);

        var first = cache.ComputeKey("clip.mp4", 100, SampleTime);
        var second = cache.ComputeKey("other.mp4", 100, SampleTime);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Changing_the_size_changes_the_key()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);

        var first = cache.ComputeKey("clip.mp4", 100, SampleTime);
        var second = cache.ComputeKey("clip.mp4", 101, SampleTime);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Changing_the_last_write_time_changes_the_key()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);

        var first = cache.ComputeKey("clip.mp4", 100, SampleTime);
        var second = cache.ComputeKey("clip.mp4", 100, SampleTime.AddSeconds(1));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Produces_a_different_key_than_the_thumbnail_cache_for_the_same_identity_inputs()
    {
        using var preview = new TemporaryDirectory();
        var hoverCache = CreateCache(preview.Path);
        var thumbnailCache = new ThumbnailCache(Options.Create(new ThumbnailCacheOptions { Path = preview.Path }));

        var hoverKey = hoverCache.ComputeKey("clip.mp4", 100, SampleTime);
        var thumbnailKey = thumbnailCache.ComputeKey("clip.mp4", 100, SampleTime);

        Assert.NotEqual(hoverKey, thumbnailKey);
    }

    [Fact]
    public void Final_and_temporary_paths_stay_within_the_hover_subdirectory_of_the_preview_root()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);
        var key = cache.ComputeKey("clip.mp4", 100, SampleTime);

        var finalPath = cache.GetFinalPath(key);
        var temporaryPath = cache.GetTemporaryPath(key);
        var expectedRoot = Path.Combine(Path.GetFullPath(preview.Path), "hover");

        Assert.StartsWith(expectedRoot, finalPath);
        Assert.StartsWith(expectedRoot, temporaryPath);
        Assert.EndsWith($"{key}.mp4", finalPath);
        Assert.Contains(".tmp.mp4", temporaryPath);
        Assert.NotEqual(finalPath, temporaryPath);
    }

    [Fact]
    public void Creating_the_cache_creates_the_hover_subdirectory()
    {
        using var preview = new TemporaryDirectory();

        CreateCache(preview.Path);

        Assert.True(Directory.Exists(Path.Combine(preview.Path, "hover")));
    }

    [Fact]
    public void Only_a_non_empty_readable_final_file_is_ready()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);
        var key = cache.ComputeKey("clip.mp4", 100, SampleTime);

        Assert.False(cache.IsReady(key));

        File.WriteAllBytes(cache.GetFinalPath(key), []);
        Assert.False(cache.IsReady(key));

        File.WriteAllBytes(cache.GetFinalPath(key), [1, 2, 3]);
        Assert.True(cache.IsReady(key));
    }

    [Fact]
    public void The_key_does_not_reveal_the_relative_path()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);

        var key = cache.ComputeKey("secret-subfolder/clip.mp4", 100, SampleTime);

        Assert.DoesNotContain("secret-subfolder", key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clip", key, StringComparison.OrdinalIgnoreCase);
    }

    private static HoverPreviewCache CreateCache(string previewPath) =>
        new(Options.Create(new ThumbnailCacheOptions { Path = previewPath }));

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-tests-{Guid.NewGuid():N}");
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
