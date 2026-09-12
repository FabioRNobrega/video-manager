using WebApp.Client.Models;

namespace WebApp.Services;

internal interface IHealthAggregationService
{
    DashboardHealthDto GetHealth();
}
