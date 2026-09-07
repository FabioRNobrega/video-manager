namespace WebApp.Configuration;

public sealed class ArchiveRootOptions
{
    public const string SectionName = "ArchiveRoot";

    public string Path { get; set; } = string.Empty;

    public static bool HasConfiguredPath(ArchiveRootOptions options) =>
        !string.IsNullOrWhiteSpace(options.Path);

    public static bool HasAbsolutePath(ArchiveRootOptions options) =>
        !HasConfiguredPath(options) || System.IO.Path.IsPathFullyQualified(options.Path);

    public static bool DirectoryExists(ArchiveRootOptions options) =>
        !HasConfiguredPath(options) || !HasAbsolutePath(options) || Directory.Exists(options.Path);

    public static bool DirectoryIsReadable(ArchiveRootOptions options)
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

    public static bool DirectoryIsWritable(ArchiveRootOptions options)
    {
        if (!HasConfiguredPath(options) || !HasAbsolutePath(options) || !Directory.Exists(options.Path))
        {
            return true;
        }

        var probePath = System.IO.Path.Combine(options.Path, $".write-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(probePath);
            Directory.Delete(probePath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool DefaultCategoriesExistOrCanBeCreated(ArchiveRootOptions options)
    {
        if (!HasConfiguredPath(options) || !HasAbsolutePath(options) || !Directory.Exists(options.Path))
        {
            return true;
        }

        try
        {
            foreach (var folderName in WebApp.Models.ArchiveCategory.Defaults.Select(category => category.FolderName))
            {
                Directory.CreateDirectory(System.IO.Path.Combine(options.Path, folderName));
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
