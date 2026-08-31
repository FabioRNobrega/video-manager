using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WebApp.Configuration;

namespace WebApp.Services;

internal sealed class ThumbnailCache(IOptions<ThumbnailCacheOptions> options)
{
    private const string VersionMarker = "v1";

    private readonly string _rootPath = Path.GetFullPath(options.Value.Path);

    public string ComputeKey(string rootRelativePath, long sizeBytes, DateTime lastWriteTimeUtc)
    {
        var normalizedPath = rootRelativePath.Replace('\\', '/');
        var identity = string.Join('|', VersionMarker, normalizedPath, sizeBytes, lastWriteTimeUtc.Ticks);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexStringLower(hash);
    }

    public string GetFinalPath(string key) => ResolveContained($"{key}.jpg");

    public string GetTemporaryPath(string key) => ResolveContained($"{key}.{Guid.NewGuid():N}.tmp.jpg");

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
            throw new InvalidOperationException("Resolved cache path escaped the configured preview root.");
        }

        return candidate;
    }
}
