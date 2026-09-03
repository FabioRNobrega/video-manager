namespace WebApp.Configuration;

public sealed class VideoCutOptions
{
    public const string SectionName = "VideoCut";

    public string Path { get; set; } = string.Empty;

    public int QueueCapacity { get; set; } = 16;

    public static bool HasConfiguredPath(VideoCutOptions options) =>
        !string.IsNullOrWhiteSpace(options.Path);

    public static bool HasAbsolutePath(VideoCutOptions options) =>
        !HasConfiguredPath(options) || System.IO.Path.IsPathFullyQualified(options.Path);

    public static bool DirectoryExists(VideoCutOptions options) =>
        !HasConfiguredPath(options) || !HasAbsolutePath(options) || Directory.Exists(options.Path);

    public static bool DirectoryIsWritable(VideoCutOptions options)
    {
        if (!HasConfiguredPath(options) || !HasAbsolutePath(options) || !Directory.Exists(options.Path))
        {
            return true;
        }

        var probePath = System.IO.Path.Combine(options.Path, $".video-cut-probe-{Guid.NewGuid():N}.tmp");
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

    public static bool HasPositiveQueueCapacity(VideoCutOptions options) =>
        options.QueueCapacity > 0;
}
