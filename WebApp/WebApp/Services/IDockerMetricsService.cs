using WebApp.Client.Models;

namespace WebApp.Services;

internal interface IDockerMetricsService
{
    Task<DashboardDockerDto> GetDockerMetricsAsync(CancellationToken cancellationToken);
}
