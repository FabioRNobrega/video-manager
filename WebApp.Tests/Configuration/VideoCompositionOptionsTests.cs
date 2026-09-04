using WebApp.Configuration;

namespace WebApp.Tests.Configuration;

public sealed class VideoCompositionOptionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("relative/composition")]
    public void Configuration_rejects_missing_or_relative_paths(string path)
    {
        var options = new VideoCompositionOptions { Path = path };

        Assert.False(VideoCompositionOptions.HasConfiguredPath(options) && VideoCompositionOptions.HasAbsolutePath(options));
    }

    [Fact]
    public void Directory_validation_accepts_existing_writable_directory()
    {
        using var directory = new TemporaryDirectory();
        var options = new VideoCompositionOptions { Path = directory.Path };

        Assert.True(VideoCompositionOptions.DirectoryExists(options));
        Assert.True(VideoCompositionOptions.DirectoryIsWritable(options));
    }

    [Fact]
    public void Queue_capacity_must_be_positive()
    {
        Assert.False(VideoCompositionOptions.HasPositiveQueueCapacity(new VideoCompositionOptions { QueueCapacity = 0 }));
        Assert.True(VideoCompositionOptions.HasPositiveQueueCapacity(new VideoCompositionOptions { QueueCapacity = 1 }));
    }

    [Fact]
    public void Transition_duration_must_be_positive()
    {
        Assert.False(VideoCompositionOptions.HasPositiveTransitionDuration(new VideoCompositionOptions { TransitionDurationSeconds = 0 }));
        Assert.True(VideoCompositionOptions.HasPositiveTransitionDuration(new VideoCompositionOptions { TransitionDurationSeconds = 5 }));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-composition-options-{Guid.NewGuid():N}");
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
