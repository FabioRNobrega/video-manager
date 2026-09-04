using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class StorageUsageServiceTests
{
    [Fact]
    public void Existing_path_returns_non_negative_usage_with_used_not_exceeding_total()
    {
        using var directory = new TemporaryDirectory();
        var service = new StorageUsageService(Options.Create(new VideoLibraryOptions { Path = directory.Path }));

        var usage = service.GetUsage();

        Assert.True(usage.UsedBytes >= 0);
        Assert.True(usage.TotalBytes >= 0);
        Assert.True(usage.UsedBytes <= usage.TotalBytes);
    }

    [Fact]
    public void Invalid_path_returns_safe_fallback_instead_of_throwing()
    {
        var service = new StorageUsageService(Options.Create(new VideoLibraryOptions { Path = string.Empty }));

        var usage = service.GetUsage();

        Assert.Equal(0, usage.UsedBytes);
        Assert.Equal(0, usage.TotalBytes);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-storage-usage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
