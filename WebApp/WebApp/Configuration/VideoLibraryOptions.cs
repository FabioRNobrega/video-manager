namespace WebApp.Configuration;

public sealed class VideoLibraryOptions
{
    public const string SectionName = "VideoLibrary";

    public string Path { get; set; } = string.Empty;

    public static bool HasConfiguredPath(VideoLibraryOptions options) =>
        !string.IsNullOrWhiteSpace(options.Path);

    public static bool HasAbsolutePath(VideoLibraryOptions options) =>
        !HasConfiguredPath(options) || System.IO.Path.IsPathFullyQualified(options.Path);

    public static bool DirectoryExists(VideoLibraryOptions options) =>
        !HasConfiguredPath(options) || !HasAbsolutePath(options) || Directory.Exists(options.Path);

    public static bool DirectoryIsReadable(VideoLibraryOptions options)
    {
        if (!HasConfiguredPath(options) || !HasAbsolutePath(options) || !Directory.Exists(options.Path))
        {
            return true;
        }

        try
        {
            using var entries = Directory.EnumerateFileSystemEntries(options.Path).GetEnumerator();
            _ = entries.MoveNext();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
