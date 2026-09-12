using WebApp.Client.Models;

namespace WebApp.Services;

internal interface INetworkMetricsService
{
    DashboardNetworkDto GetNetworkMetrics();
}
