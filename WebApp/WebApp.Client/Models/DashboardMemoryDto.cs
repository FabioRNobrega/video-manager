namespace WebApp.Client.Models;

public sealed record DashboardMemoryDto(
    bool IsAvailable,
    long? UsedBytes,
    long? TotalBytes,
    long? AvailableBytes,
    long? CachedBytes,
    long? SwapUsedBytes,
    long? SwapTotalBytes);
