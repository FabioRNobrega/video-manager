namespace WebApp.Client.Models;

public sealed record DashboardHistorySampleDto(
    DateTime TimestampUtc,
    double? CpuPercent,
    double? MemoryPercent,
    double? NetworkBytesPerSecond,
    double? DiskUsedPercent,
    double? TemperatureCelsius);
