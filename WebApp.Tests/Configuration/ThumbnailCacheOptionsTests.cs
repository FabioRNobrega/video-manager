using WebApp.Configuration;

namespace WebApp.Tests.Configuration;

public sealed class ThumbnailCacheOptionsTests
{
    [Fact]
    public void Accepts_an_absolute_existing_writable_directory_disjoint_from_the_video_root()
    {
        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var options = new ThumbnailCacheOptions { Path = preview.Path };

        Assert.True(ThumbnailCacheOptions.HasConfiguredPath(options));
        Assert.True(ThumbnailCacheOptions.HasAbsolutePath(options));
        Assert.True(ThumbnailCacheOptions.DirectoryExists(options));
        Assert.True(ThumbnailCacheOptions.DirectoryIsWritable(options));
        Assert.True(ThumbnailCacheOptions.IsDisjointFromVideoRoot(options, videos.Path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/preview")]
    public void Rejects_blank_or_relative_paths(string path)
    {
        var options = new ThumbnailCacheOptions { Path = path };

        Assert.False(ThumbnailCacheOptions.HasConfiguredPath(options) && ThumbnailCacheOptions.HasAbsolutePath(options));
    }

    [Fact]
    public void Rejects_a_nonexistent_directory()
    {
        var options = new ThumbnailCacheOptions
        {
            Path = Path.Combine(Path.GetTempPath(), $"video-manager-missing-preview-{Guid.NewGuid():N}")
        };

        Assert.False(ThumbnailCacheOptions.DirectoryExists(options));
    }

    [Fact]
    public void Rejects_a_directory_that_cannot_be_written_to()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var preview = new TemporaryDirectory();
        File.SetUnixFileMode(preview.Path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var options = new ThumbnailCacheOptions { Path = preview.Path };
            if (ThumbnailCacheOptions.DirectoryIsWritable(options))
            {
                // Running as root (e.g. inside the dev container) ignores the permission bits.
                return;
            }

            Assert.False(ThumbnailCacheOptions.DirectoryIsWritable(options));
        }
        finally
        {
            File.SetUnixFileMode(preview.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void Writability_probe_leaves_no_residue()
    {
        using var preview = new TemporaryDirectory();
        var options = new ThumbnailCacheOptions { Path = preview.Path };

        Assert.True(ThumbnailCacheOptions.DirectoryIsWritable(options));
        Assert.Empty(Directory.EnumerateFileSystemEntries(preview.Path));
    }

    [Fact]
    public void Rejects_a_preview_path_equal_to_the_video_root()
    {
        using var shared = new TemporaryDirectory();
        var options = new ThumbnailCacheOptions { Path = shared.Path };

        Assert.False(ThumbnailCacheOptions.IsDisjointFromVideoRoot(options, shared.Path));
    }

    [Fact]
    public void Rejects_a_preview_path_nested_inside_the_video_root()
    {
        using var videos = new TemporaryDirectory();
        var nestedPreview = Directory.CreateDirectory(Path.Combine(videos.Path, "previews")).FullName;
        var options = new ThumbnailCacheOptions { Path = nestedPreview };

        Assert.False(ThumbnailCacheOptions.IsDisjointFromVideoRoot(options, videos.Path));
    }

    [Fact]
    public void Rejects_a_video_root_nested_inside_the_preview_path()
    {
        using var preview = new TemporaryDirectory();
        var nestedVideos = Directory.CreateDirectory(Path.Combine(preview.Path, "videos")).FullName;
        var options = new ThumbnailCacheOptions { Path = preview.Path };

        Assert.False(ThumbnailCacheOptions.IsDisjointFromVideoRoot(options, nestedVideos));
    }

    [Fact]
    public void Allows_sibling_directories_with_overlapping_name_prefixes()
    {
        using var parent = new TemporaryDirectory();
        var previewPath = Directory.CreateDirectory(Path.Combine(parent.Path, "root")).FullName;
        var videoPath = Directory.CreateDirectory(Path.Combine(parent.Path, "root-videos")).FullName;
        var options = new ThumbnailCacheOptions { Path = previewPath };

        Assert.True(ThumbnailCacheOptions.IsDisjointFromVideoRoot(options, videoPath));
    }

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
