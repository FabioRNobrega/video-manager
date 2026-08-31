using WebApp.Configuration;

namespace WebApp.Tests.Configuration;

public sealed class HoverPreviewOptionsTests
{
    [Fact]
    public void Documented_defaults_are_accepted()
    {
        var options = new HoverPreviewOptions();

        Assert.True(options.Enabled);
        Assert.Equal(480, options.Width);
        Assert.Equal(15, options.FrameRate);
        Assert.Equal(1.5, options.SegmentSeconds);
        Assert.Equal(8, options.QueueCapacity);
        Assert.True(HoverPreviewOptions.HasPositiveWidth(options));
        Assert.True(HoverPreviewOptions.HasPositiveFrameRate(options));
        Assert.True(HoverPreviewOptions.HasPositiveSegmentSeconds(options));
        Assert.True(HoverPreviewOptions.HasPositiveQueueCapacity(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_width(int width)
    {
        var options = new HoverPreviewOptions { Width = width };

        Assert.False(HoverPreviewOptions.HasPositiveWidth(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_frame_rate(int frameRate)
    {
        var options = new HoverPreviewOptions { FrameRate = frameRate };

        Assert.False(HoverPreviewOptions.HasPositiveFrameRate(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void Rejects_non_positive_segment_seconds(double segmentSeconds)
    {
        var options = new HoverPreviewOptions { SegmentSeconds = segmentSeconds };

        Assert.False(HoverPreviewOptions.HasPositiveSegmentSeconds(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_queue_capacity(int queueCapacity)
    {
        var options = new HoverPreviewOptions { QueueCapacity = queueCapacity };

        Assert.False(HoverPreviewOptions.HasPositiveQueueCapacity(options));
    }
}
