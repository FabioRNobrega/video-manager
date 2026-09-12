using WebApp.Client.Models;

namespace WebApp.Services;

internal sealed class DockerMetricsService(IDockerApiClient dockerApiClient) : IDockerMetricsService
{
    public async Task<DashboardDockerDto> GetDockerMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var samples = await dockerApiClient.ListContainersAsync(cancellationToken);
            var containers = samples
                .Select(sample => new DashboardDockerContainerDto(
                    sample.Name, sample.CpuPercent, sample.MemoryUsedBytes, sample.UptimeSeconds, sample.RestartCount))
                .ToList();

            return new DashboardDockerDto(true, containers);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new DashboardDockerDto(false, []);
        }
    }
}
