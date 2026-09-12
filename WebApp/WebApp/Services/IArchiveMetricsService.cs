using WebApp.Client.Models;

namespace WebApp.Services;

internal interface IArchiveMetricsService
{
    DashboardArchiveDto GetArchiveMetrics();
}
