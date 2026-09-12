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
        var service = new StorageUsageService(Options.Create(new ArchiveRootOptions { Path = directory.Path }));

        var usage = service.GetUsage();

        Assert.True(usage.UsedBytes >= 0);
        Assert.True(usage.TotalBytes >= 0);
        Assert.True(usage.UsedBytes <= usage.TotalBytes);
    }

    [Fact]
    public void Invalid_path_returns_safe_fallback_instead_of_throwing()
    {
        var service = new StorageUsageService(Options.Create(new ArchiveRootOptions { Path = string.Empty }));

        var usage = service.GetUsage();

        Assert.Equal(0, usage.UsedBytes);
        Assert.Equal(0, usage.TotalBytes);
    }

    [Fact]
    public void ParseDiskStats_sums_sectors_across_devices_excluding_loop_and_ram()
    {
        const string diskStats =
            "   8       0 sda 100 0 2000 0 50 0 1000 0 0 0 0\n" +
            "   7       0 loop0 999 0 999999 0 999 0 999999 0 0 0 0\n" +
            "  253       0 ram0 999 0 999999 0 999 0 999999 0 0 0 0\n";

        var totals = StorageUsageService.ParseDiskStats(diskStats);

        Assert.NotNull(totals);
        Assert.Equal(2000, totals!.Value.ReadSectors);
        Assert.Equal(1000, totals.Value.WriteSectors);
    }

    [Fact]
    public void ParseDiskStats_returns_null_for_content_with_no_recognizable_devices()
    {
        Assert.Null(StorageUsageService.ParseDiskStats("not-diskstats-content"));
    }

    [Fact]
    public void GetThroughput_returns_non_negative_values_or_null_when_unavailable()
    {
        using var directory = new TemporaryDirectory();
        var service = new StorageUsageService(Options.Create(new ArchiveRootOptions { Path = directory.Path }));

        var (readBytesPerSecond, writeBytesPerSecond) = service.GetThroughput();

        Assert.True(readBytesPerSecond is null or >= 0);
        Assert.True(writeBytesPerSecond is null or >= 0);
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
