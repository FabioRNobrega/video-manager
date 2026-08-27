using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class VideoLibraryService(IOptions<VideoLibraryOptions> options) : IVideoLibraryService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm", ".mov", ".m4v" };

    private readonly string _rootPath = Path.GetFullPath(options.Value.Path);
    private IReadOnlyDictionary<string, VideoFileEntry> _snapshot =
        new Dictionary<string, VideoFileEntry>(StringComparer.Ordinal);

    public async Task<IReadOnlyList<VideoFileEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await Task.Run(() => Discover(cancellationToken), cancellationToken);
            var snapshot = entries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
            Interlocked.Exchange(ref _snapshot, snapshot);
            return entries;
        }
        catch
        {
            Interlocked.Exchange(
                ref _snapshot,
                new Dictionary<string, VideoFileEntry>(StringComparer.Ordinal));
            throw;
        }
    }

    public bool TryResolve(string id, out VideoFileEntry? entry)
    {
        entry = null;
        return IsOpaqueId(id) && Volatile.Read(ref _snapshot).TryGetValue(id, out entry);
    }

    internal static bool IsWithinRoot(string rootPath, string candidatePath)
    {
        var canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var canonicalCandidate = Path.GetFullPath(candidatePath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootPrefix = canonicalRoot + Path.DirectorySeparatorChar;

        return canonicalCandidate.StartsWith(rootPrefix, comparison);
    }

    private List<VideoFileEntry> Discover(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Accessing the root is a scan-level failure. Errors beneath it are treated as
        // transient entries and skipped so one changing file doesn't poison the library.
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(_rootPath);
        var discovered = new List<VideoFileEntry>();
        var isRoot = true;

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pendingDirectories.Pop();
            IEnumerable<string> children;

            try
            {
                children = Directory.EnumerateFileSystemEntries(directory).ToArray();
            }
            catch (Exception exception) when (
                !isRoot && exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                isRoot = false;
                continue;
            }

            isRoot = false;

            foreach (var child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var attributes = File.GetAttributes(child);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    var canonicalPath = Path.GetFullPath(child);
                    if (!IsWithinRoot(_rootPath, canonicalPath))
                    {
                        continue;
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pendingDirectories.Push(canonicalPath);
                        continue;
                    }

                    var extension = Path.GetExtension(canonicalPath);
                    if (!SupportedExtensions.Contains(extension))
                    {
                        continue;
                    }

                    var file = new FileInfo(canonicalPath);
                    using (File.Open(canonicalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                    }

                    discovered.Add(new VideoFileEntry(
                        Guid.NewGuid().ToString("N"),
                        canonicalPath,
                        file.Name,
                        extension.ToLowerInvariant(),
                        file.Length));
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
                {
                    // Files may disappear or become unreadable while an explicit scan is running.
                }
            }
        }

        return discovered
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsOpaqueId(string id) =>
        id.Length == 32 && Guid.TryParseExact(id, "N", out _);
}
