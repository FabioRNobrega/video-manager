using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class VideoCutService(IOptions<VideoCutOptions> options) : IVideoCutService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm", ".mov", ".m4v" };

    private static readonly SnapshotState EmptySnapshot =
        new(new Dictionary<string, VideoFileEntry>(StringComparer.Ordinal), []);

    private readonly string _rootPath = Path.GetFullPath(options.Value.Path);
    private SnapshotState _snapshot = EmptySnapshot;

    public async Task<IReadOnlyList<VideoFileEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await Task.Run(() => Discover(cancellationToken), cancellationToken);
            var snapshot = new SnapshotState(entries.ToDictionary(entry => entry.Id, StringComparer.Ordinal), entries);
            Interlocked.Exchange(ref _snapshot, snapshot);
            return entries;
        }
        catch
        {
            Interlocked.Exchange(ref _snapshot, EmptySnapshot);
            throw;
        }
    }

    public IReadOnlyList<VideoFileEntry> GetCurrentSnapshot() => Volatile.Read(ref _snapshot).Ordered;

    public bool TryResolve(string id, out VideoFileEntry? entry)
    {
        entry = null;
        return IsOpaqueId(id) && Volatile.Read(ref _snapshot).ById.TryGetValue(id, out entry);
    }

    private List<VideoFileEntry> Discover(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var discovered = new List<VideoFileEntry>();
        foreach (var child in Directory.EnumerateFileSystemEntries(_rootPath).ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var attributes = File.GetAttributes(child);
                if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                    (attributes & FileAttributes.Directory) != 0)
                {
                    continue;
                }

                var canonicalPath = Path.GetFullPath(child);
                if (!VideoLibraryService.IsWithinRoot(_rootPath, canonicalPath))
                {
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
                    file.Name,
                    extension.ToLowerInvariant(),
                    file.Length,
                    file.LastWriteTimeUtc));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
            {
            }
        }

        return discovered
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsOpaqueId(string id) =>
        id.Length == 32 && Guid.TryParseExact(id, "N", out _);

    private sealed record SnapshotState(
        IReadOnlyDictionary<string, VideoFileEntry> ById,
        IReadOnlyList<VideoFileEntry> Ordered);
}
