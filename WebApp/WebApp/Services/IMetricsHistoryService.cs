using WebApp.Client.Models;

namespace WebApp.Services;

internal interface IMetricsHistoryService
{
    DashboardHistoryDto GetHistory();
}
