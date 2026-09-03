using WebApp.Configuration;

namespace WebApp.Tests.Configuration;

public sealed class VideoCutOptionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("relative/cuts")]
    public void Configuration_rejects_missing_or_relative_paths(string path)
    {
        var options = new VideoCutOptions { Path = path };

        Assert.False(VideoCutOptions.HasConfiguredPath(options) && VideoCutOptions.HasAbsolutePath(options));
    }

    [Fact]
    public void Directory_validation_accepts_existing_writable_directory()
    {
        using var directory = new TemporaryDirectory();
        var options = new VideoCutOptions { Path = directory.Path };

        Assert.True(VideoCutOptions.DirectoryExists(options));
        Assert.True(VideoCutOptions.DirectoryIsWritable(options));
    }

    [Fact]
    public void Queue_capacity_must_be_positive()
    {
        Assert.False(VideoCutOptions.HasPositiveQueueCapacity(new VideoCutOptions { QueueCapacity = 0 }));
        Assert.True(VideoCutOptions.HasPositiveQueueCapacity(new VideoCutOptions { QueueCapacity = 1 }));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-cut-options-{Guid.NewGuid():N}");
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
