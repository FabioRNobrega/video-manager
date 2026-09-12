using WebApp.Client.Models;

namespace WebApp.Services;

internal sealed class MetricsHistoryBackgroundWorker(
    ISystemMetricsService systemMetricsService,
    IStorageUsageService storageUsageService,
    INetworkMetricsService networkMetricsService,
    ILogger<MetricsHistoryBackgroundWorker> logger) : BackgroundService, IMetricsHistoryService
{
    internal const int MaxSamples = 15;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private readonly Queue<DashboardHistorySampleDto> _samples = new(MaxSamples);

    public DashboardHistoryDto GetHistory()
    {
        lock (_gate)
        {
            return new DashboardHistoryDto(_samples.ToList());
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SampleInterval);

        do
        {
            try
            {
                CollectSample();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Failed to collect a dashboard metrics history sample.");
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    internal void CollectSample()
    {
        var system = systemMetricsService.GetSystemMetrics();
        var memory = systemMetricsService.GetMemoryMetrics();
        var storage = storageUsageService.GetUsage();
        var network = networkMetricsService.GetNetworkMetrics();

        double? memoryPercent = memory is { IsAvailable: true, TotalBytes: > 0 }
            ? memory.UsedBytes!.Value * 100.0 / memory.TotalBytes!.Value
            : null;
        double? diskPercent = storage.TotalBytes > 0
            ? storage.UsedBytes * 100.0 / storage.TotalBytes
            : null;
        double? networkBytesPerSecond = network is { IsAvailable: true }
            ? (network.UploadBytesPerSecond ?? 0) + (network.DownloadBytesPerSecond ?? 0)
            : null;

        var sample = new DashboardHistorySampleDto(
            DateTime.UtcNow,
            system.CpuPercent,
            memoryPercent,
            networkBytesPerSecond,
            diskPercent,
            system.TemperatureCelsius);

        lock (_gate)
        {
            if (_samples.Count >= MaxSamples)
            {
                _samples.Dequeue();
            }

            _samples.Enqueue(sample);
        }
    }
}
