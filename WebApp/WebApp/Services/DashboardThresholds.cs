using WebApp.Client.Models;

namespace WebApp.Services;

internal static class DashboardThresholds
{
    public const double WarningPercent = 70.0;
    public const double CriticalPercent = 90.0;

    public static DashboardHealthStatus Evaluate(double? percent)
    {
        if (percent is null)
        {
            return DashboardHealthStatus.Unavailable;
        }

        if (percent >= CriticalPercent)
        {
            return DashboardHealthStatus.Critical;
        }

        return percent >= WarningPercent ? DashboardHealthStatus.Warning : DashboardHealthStatus.Ok;
    }
}
