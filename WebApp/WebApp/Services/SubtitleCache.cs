using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class SubtitleCache
{
    private const string VersionMarker = "subtitlev1";

    private readonly string _rootPath;

    public SubtitleCache(IOptions<ThumbnailCacheOptions> thumbnailCacheOptions)
    {
        _rootPath = Path.GetFullPath(Path.Combine(thumbnailCacheOptions.Value.Path, "subtitles"));
        Directory.CreateDirectory(_rootPath);
    }

    public string ComputeKey(VideoFileEntry entry, SubtitleFileInfo subtitle)
    {
        var normalizedPath = entry.RelativePath.Replace('\\', '/');
        var identity = string.Join(
            '|',
            VersionMarker,
            normalizedPath,
            entry.SizeBytes,
            entry.LastWriteTimeUtc.Ticks,
            subtitle.SizeBytes,
            subtitle.LastWriteTimeUtc.Ticks);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexStringLower(hash);
    }

    public string GetFinalPath(string key) => ResolveContained($"{key}.vtt");

    public string GetTemporaryPath(string key) => ResolveContained($"{key}.{Guid.NewGuid():N}.tmp.vtt");

    public bool IsReady(string key)
    {
        try
        {
            var file = new FileInfo(GetFinalPath(key));
            return file.Exists && file.Length > 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string ResolveContained(string fileName)
    {
        var candidate = Path.GetFullPath(Path.Combine(_rootPath, fileName));
        if (!VideoLibraryService.IsWithinRoot(_rootPath, candidate))
        {
            throw new InvalidOperationException("Resolved subtitle cache path escaped the configured preview root.");
        }

        return candidate;
    }
}
