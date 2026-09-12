using WebApp.Client.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class DashboardThresholdsTests
{
    [Theory]
    [InlineData(50.0, DashboardHealthStatus.Ok)]
    [InlineData(75.0, DashboardHealthStatus.Warning)]
    [InlineData(95.0, DashboardHealthStatus.Critical)]
    public void Evaluate_matches_documented_thresholds(double percent, DashboardHealthStatus expected)
    {
        Assert.Equal(expected, DashboardThresholds.Evaluate(percent));
    }

    [Fact]
    public void Evaluate_returns_unavailable_for_null_percent()
    {
        Assert.Equal(DashboardHealthStatus.Unavailable, DashboardThresholds.Evaluate(null));
    }
}
