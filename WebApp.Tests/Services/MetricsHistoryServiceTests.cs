using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Client.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class MetricsHistoryServiceTests
{
    [Fact]
    public void Fresh_instance_returns_empty_but_valid_history()
    {
        var worker = CreateWorker();

        var history = worker.GetHistory();

        Assert.NotNull(history);
        Assert.Empty(history.Samples);
    }

    [Fact]
    public void Ring_buffer_evicts_oldest_sample_once_capacity_is_exceeded()
    {
        var worker = CreateWorker();

        for (var i = 0; i < MetricsHistoryBackgroundWorker.MaxSamples + 5; i++)
        {
            worker.CollectSample();
        }

        var history = worker.GetHistory();

        Assert.Equal(MetricsHistoryBackgroundWorker.MaxSamples, history.Samples.Count);
    }

    private static MetricsHistoryBackgroundWorker CreateWorker() => new(
        new FakeSystemMetricsService(),
        new FakeStorageUsageService(),
        new FakeNetworkMetricsService(),
        NullLogger<MetricsHistoryBackgroundWorker>.Instance);

    private sealed class FakeSystemMetricsService : ISystemMetricsService
    {
        public DashboardSystemDto GetSystemMetrics() => new(true, 10, 0.1, 0.1, 0.1, 40, 1000);

        public DashboardMemoryDto GetMemoryMetrics() => new(true, 100, 1000, 900, 50, 0, 0);
    }

    private sealed class FakeStorageUsageService : IStorageUsageService
    {
        public StorageUsageDto GetUsage() => new(10, 100);

        public (double? ReadBytesPerSecond, double? WriteBytesPerSecond) GetThroughput() => (0, 0);
    }

    private sealed class FakeNetworkMetricsService : INetworkMetricsService
    {
        public DashboardNetworkDto GetNetworkMetrics() => new(true, 10, 10, 0, 0, []);
    }
}
