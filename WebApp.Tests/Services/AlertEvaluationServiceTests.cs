using WebApp.Client.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class AlertEvaluationServiceTests
{
    private readonly AlertEvaluationService _service = new();

    [Fact]
    public void No_alert_when_all_metrics_below_warning_threshold()
    {
        var alerts = _service.Evaluate(new DashboardAlertInputs(50, 50, 50));

        Assert.Empty(alerts);
    }

    [Fact]
    public void Warning_alert_when_metric_between_warning_and_critical_thresholds()
    {
        var alerts = _service.Evaluate(new DashboardAlertInputs(75, null, null));

        var alert = Assert.Single(alerts);
        Assert.Equal(DashboardAlertSeverity.Warning, alert.Severity);
        Assert.Contains("Storage", alert.Message);
    }

    [Fact]
    public void Critical_alert_when_metric_at_or_above_critical_threshold()
    {
        var alerts = _service.Evaluate(new DashboardAlertInputs(null, 95, null));

        var alert = Assert.Single(alerts);
        Assert.Equal(DashboardAlertSeverity.Critical, alert.Severity);
        Assert.Contains("Memory", alert.Message);
    }

    [Fact]
    public void Null_metrics_produce_no_alerts()
    {
        var alerts = _service.Evaluate(new DashboardAlertInputs(null, null, null));

        Assert.Empty(alerts);
    }
}
