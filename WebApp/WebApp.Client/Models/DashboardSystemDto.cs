namespace WebApp.Client.Models;

public sealed record DashboardSystemDto(
    bool IsAvailable,
    double? CpuPercent,
    double? LoadAverage1m,
    double? LoadAverage5m,
    double? LoadAverage15m,
    double? TemperatureCelsius,
    double? UptimeSeconds);
