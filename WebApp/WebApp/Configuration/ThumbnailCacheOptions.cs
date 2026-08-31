namespace WebApp.Configuration;

public sealed class ThumbnailCacheOptions
{
    public const string SectionName = "ThumbnailCache";

    public string Path { get; set; } = string.Empty;

    public int QueueCapacity { get; set; } = 64;

    public static bool HasConfiguredPath(ThumbnailCacheOptions options) =>
        !string.IsNullOrWhiteSpace(options.Path);

    public static bool HasAbsolutePath(ThumbnailCacheOptions options) =>
        !HasConfiguredPath(options) || System.IO.Path.IsPathFullyQualified(options.Path);

    public static bool DirectoryExists(ThumbnailCacheOptions options) =>
        !HasConfiguredPath(options) || !HasAbsolutePath(options) || Directory.Exists(options.Path);

    public static bool DirectoryIsWritable(ThumbnailCacheOptions options)
    {
        if (!HasConfiguredPath(options) || !HasAbsolutePath(options) || !Directory.Exists(options.Path))
        {
            return true;
        }

        var probePath = System.IO.Path.Combine(options.Path, $".thumbnail-cache-probe-{Guid.NewGuid():N}.tmp");
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

    public static bool IsDisjointFromVideoRoot(ThumbnailCacheOptions options, string? videoRootPath)
    {
        if (!HasConfiguredPath(options) || !HasAbsolutePath(options) || !Directory.Exists(options.Path))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(videoRootPath) || !System.IO.Path.IsPathFullyQualified(videoRootPath))
        {
            return true;
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var previewRoot = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(options.Path));
        var videoRoot = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(videoRootPath));

        if (string.Equals(previewRoot, videoRoot, comparison))
        {
            return false;
        }

        return !previewRoot.StartsWith(videoRoot + System.IO.Path.DirectorySeparatorChar, comparison)
            && !videoRoot.StartsWith(previewRoot + System.IO.Path.DirectorySeparatorChar, comparison);
    }
}
