namespace WebApp.Client.Models;

public sealed record DashboardNetworkDto(
    bool IsAvailable,
    double? UploadBytesPerSecond,
    double? DownloadBytesPerSecond,
    long TotalErrors,
    long TotalDrops,
    IReadOnlyList<DashboardNetworkInterfaceDto> Interfaces);
