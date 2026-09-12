namespace WebApp.Services;

internal readonly record struct DockerContainerSample(
    string Name,
    double CpuPercent,
    long MemoryUsedBytes,
    double UptimeSeconds,
    int RestartCount);

internal interface IDockerApiClient
{
    Task<IReadOnlyList<DockerContainerSample>> ListContainersAsync(CancellationToken cancellationToken);
}
