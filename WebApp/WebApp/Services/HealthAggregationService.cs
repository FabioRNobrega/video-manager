using WebApp.Client.Models;

namespace WebApp.Services;

internal sealed class HealthAggregationService(
    IStorageUsageService storageUsageService,
    IFfmpegAvailabilityProbe ffmpegAvailabilityProbe) : IHealthAggregationService
{
    public DashboardHealthDto GetHealth()
    {
        var usage = storageUsageService.GetUsage();
        var storageHealth = usage.TotalBytes > 0
            ? DashboardThresholds.Evaluate(usage.UsedBytes * 100.0 / usage.TotalBytes)
            : DashboardHealthStatus.Unavailable;

        const DashboardHealthStatus applicationHealth = DashboardHealthStatus.Ok;
        var dependencyHealth = ffmpegAvailabilityProbe.IsAvailable()
            ? DashboardHealthStatus.Ok
            : DashboardHealthStatus.Critical;
        const DashboardHealthStatus backupHealth = DashboardHealthStatus.NotConfigured;

        var overall = ComputeOverall(storageHealth, applicationHealth, dependencyHealth, backupHealth);

        return new DashboardHealthDto(storageHealth, applicationHealth, dependencyHealth, backupHealth, overall);
    }

    internal static DashboardHealthStatus ComputeOverall(params DashboardHealthStatus[] statuses) =>
        statuses.MaxBy(Severity);

    private static int Severity(DashboardHealthStatus status) => status switch
    {
        DashboardHealthStatus.Critical => 4,
        DashboardHealthStatus.Warning => 3,
        DashboardHealthStatus.Unavailable => 2,
        DashboardHealthStatus.NotConfigured => 1,
        DashboardHealthStatus.Ok => 0,
        _ => 0,
    };
}
