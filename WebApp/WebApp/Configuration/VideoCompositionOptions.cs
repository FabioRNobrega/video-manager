namespace WebApp.Configuration;

public sealed class VideoCompositionOptions
{
    public const string SectionName = "VideoComposition";

    public string Path { get; set; } = string.Empty;

    public int QueueCapacity { get; set; } = 16;

    public double TransitionDurationSeconds { get; set; } = 5;

    public static bool HasConfiguredPath(VideoCompositionOptions options) =>
        !string.IsNullOrWhiteSpace(options.Path);

    public static bool HasAbsolutePath(VideoCompositionOptions options) =>
        !HasConfiguredPath(options) || System.IO.Path.IsPathFullyQualified(options.Path);

    public static bool DirectoryExists(VideoCompositionOptions options) =>
        !HasConfiguredPath(options) || !HasAbsolutePath(options) || Directory.Exists(options.Path);

    public static bool DirectoryIsWritable(VideoCompositionOptions options)
    {
        if (!HasConfiguredPath(options) || !HasAbsolutePath(options) || !Directory.Exists(options.Path))
        {
            return true;
        }

        var probePath = System.IO.Path.Combine(options.Path, $".video-composition-probe-{Guid.NewGuid():N}.tmp");
        try
        {
            using (File.Create(probePath))
            {
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public static bool HasPositiveQueueCapacity(VideoCompositionOptions options) =>
        options.QueueCapacity > 0;

    public static bool HasPositiveTransitionDuration(VideoCompositionOptions options) =>
        options.TransitionDurationSeconds > 0;
}
