using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class ThumbnailCacheTests
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
    public void Backslash_and_forward_slash_relative_paths_normalize_to_the_same_key()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);

        var withForwardSlash = cache.ComputeKey("nested/clip.mp4", 100, SampleTime);
        var withBackslash = cache.ComputeKey("nested\\clip.mp4", 100, SampleTime);

        Assert.Equal(withForwardSlash, withBackslash);
    }

    [Fact]
    public void Final_and_temporary_paths_stay_within_the_preview_root()
    {
        using var preview = new TemporaryDirectory();
        var cache = CreateCache(preview.Path);
        var key = cache.ComputeKey("clip.mp4", 100, SampleTime);

        var finalPath = cache.GetFinalPath(key);
        var temporaryPath = cache.GetTemporaryPath(key);

        Assert.StartsWith(Path.GetFullPath(preview.Path), finalPath);
        Assert.StartsWith(Path.GetFullPath(preview.Path), temporaryPath);
        Assert.EndsWith($"{key}.jpg", finalPath);
        Assert.Contains(".tmp.jpg", temporaryPath);
        Assert.NotEqual(finalPath, temporaryPath);
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

    private static ThumbnailCache CreateCache(string previewPath) =>
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
