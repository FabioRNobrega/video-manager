namespace WebApp.Client.Models;

public sealed record DashboardStorageDto(
    bool IsAvailable,
    long UsedBytes,
    long TotalBytes,
    double? ReadBytesPerSecond,
    double? WriteBytesPerSecond,
    DashboardHealthStatus Health);
