using WebApp.Client.Models;

namespace WebApp.Services;

internal sealed class AlertEvaluationService : IAlertEvaluationService
{
    public IReadOnlyList<DashboardAlertDto> Evaluate(DashboardAlertInputs inputs)
    {
        var alerts = new List<DashboardAlertDto>();

        AddIfExceeded(alerts, "Storage", inputs.StoragePercent);
        AddIfExceeded(alerts, "Memory", inputs.MemoryPercent);
        AddIfExceeded(alerts, "CPU", inputs.CpuPercent);

        return alerts;
    }

    private static void AddIfExceeded(List<DashboardAlertDto> alerts, string metricName, double? percent)
    {
        if (percent is null)
        {
            return;
        }

        if (percent >= DashboardThresholds.CriticalPercent)
        {
            alerts.Add(new DashboardAlertDto(
                DashboardAlertSeverity.Critical,
                $"{metricName} usage is at {percent:0}%, above the {DashboardThresholds.CriticalPercent:0}% critical threshold."));
        }
        else if (percent >= DashboardThresholds.WarningPercent)
        {
            alerts.Add(new DashboardAlertDto(
                DashboardAlertSeverity.Warning,
                $"{metricName} usage is at {percent:0}%, above the {DashboardThresholds.WarningPercent:0}% warning threshold."));
        }
    }
}
