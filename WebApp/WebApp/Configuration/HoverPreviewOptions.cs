namespace WebApp.Configuration;

public sealed class HoverPreviewOptions
{
    public const string SectionName = "HoverPreview";

    public bool Enabled { get; set; } = true;

    public int Width { get; set; } = 480;

    public int FrameRate { get; set; } = 15;

    public double SegmentSeconds { get; set; } = 1.5;

    public int QueueCapacity { get; set; } = 8;

    public static bool HasPositiveWidth(HoverPreviewOptions options) => options.Width > 0;

    public static bool HasPositiveFrameRate(HoverPreviewOptions options) => options.FrameRate > 0;

    public static bool HasPositiveSegmentSeconds(HoverPreviewOptions options) => options.SegmentSeconds > 0;

    public static bool HasPositiveQueueCapacity(HoverPreviewOptions options) => options.QueueCapacity > 0;
}
