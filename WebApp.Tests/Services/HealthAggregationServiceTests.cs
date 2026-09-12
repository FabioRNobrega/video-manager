using WebApp.Client.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class HealthAggregationServiceTests
{
    [Fact]
    public void Overall_is_ok_when_all_substatuses_are_healthy()
    {
        var service = new HealthAggregationService(
            new FakeStorageUsageService(usedBytes: 10, totalBytes: 100),
            new FakeFfmpegAvailabilityProbe(isAvailable: true));

        var health = service.GetHealth();

        Assert.Equal(DashboardHealthStatus.Ok, health.Storage);
        Assert.Equal(DashboardHealthStatus.Ok, health.Application);
        Assert.Equal(DashboardHealthStatus.Ok, health.Dependencies);
        Assert.Equal(DashboardHealthStatus.NotConfigured, health.Backups);
        Assert.Equal(DashboardHealthStatus.NotConfigured, health.Overall);
    }

    [Fact]
    public void Overall_is_critical_when_any_substatus_is_critical()
    {
        var service = new HealthAggregationService(
            new FakeStorageUsageService(usedBytes: 95, totalBytes: 100),
            new FakeFfmpegAvailabilityProbe(isAvailable: true));

        var health = service.GetHealth();

        Assert.Equal(DashboardHealthStatus.Critical, health.Storage);
        Assert.Equal(DashboardHealthStatus.Critical, health.Overall);
    }

    [Fact]
    public void Dependencies_are_critical_when_ffmpeg_is_unavailable()
    {
        var service = new HealthAggregationService(
            new FakeStorageUsageService(usedBytes: 10, totalBytes: 100),
            new FakeFfmpegAvailabilityProbe(isAvailable: false));

        var health = service.GetHealth();

        Assert.Equal(DashboardHealthStatus.Critical, health.Dependencies);
        Assert.Equal(DashboardHealthStatus.Critical, health.Overall);
    }

    private sealed class FakeStorageUsageService(long usedBytes, long totalBytes) : IStorageUsageService
    {
        public WebApp.Client.Models.StorageUsageDto GetUsage() => new(usedBytes, totalBytes);

        public (double? ReadBytesPerSecond, double? WriteBytesPerSecond) GetThroughput() => (null, null);
    }

    private sealed class FakeFfmpegAvailabilityProbe(bool isAvailable) : IFfmpegAvailabilityProbe
    {
        public bool IsAvailable() => isAvailable;
    }
}
