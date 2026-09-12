namespace WebApp.Client.Models;

public sealed record DashboardDockerContainerDto(
    string Name,
    double CpuPercent,
    long MemoryUsedBytes,
    double UptimeSeconds,
    int RestartCount);
