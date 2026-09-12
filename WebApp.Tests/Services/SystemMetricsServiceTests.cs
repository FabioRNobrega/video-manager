using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class SystemMetricsServiceTests
{
    private const string StatFixtureOne =
        "cpu  100 0 100 800 0 0 0 0 0 0\ncpu0 100 0 100 800 0 0 0 0 0 0\n";
    private const string StatFixtureTwo =
        "cpu  150 0 150 900 0 0 0 0 0 0\ncpu0 150 0 150 900 0 0 0 0 0 0\n";

    [Fact]
    public void ParseCpuStatSample_reads_idle_and_total_ticks()
    {
        var sample = SystemMetricsService.ParseCpuStatSample(StatFixtureOne);

        Assert.NotNull(sample);
        Assert.Equal(800, sample!.Value.IdleTicks);
        Assert.Equal(1000, sample.Value.TotalTicks);
    }

    [Fact]
    public void ComputeCpuPercent_returns_percent_used_between_two_samples()
    {
        var first = SystemMetricsService.ParseCpuStatSample(StatFixtureOne)!.Value;
        var second = SystemMetricsService.ParseCpuStatSample(StatFixtureTwo)!.Value;

        var percent = SystemMetricsService.ComputeCpuPercent(first, second);

        Assert.NotNull(percent);
        Assert.InRange(percent!.Value, 0, 100);
    }

    [Fact]
    public void ParseLoadAverage_reads_first_three_values()
    {
        var loadAverage = SystemMetricsService.ParseLoadAverage("0.10 0.05 0.01 1/200 12345");

        Assert.NotNull(loadAverage);
        Assert.Equal((0.10, 0.05, 0.01), loadAverage!.Value);
    }

    [Fact]
    public void ParseLoadAverage_returns_null_for_malformed_content()
    {
        Assert.Null(SystemMetricsService.ParseLoadAverage("not-a-number"));
    }

    [Fact]
    public void ParseUptimeSeconds_reads_first_value()
    {
        var uptime = SystemMetricsService.ParseUptimeSeconds("12345.67 98765.43");

        Assert.Equal(12345.67, uptime);
    }

    [Fact]
    public void ParseTemperatureCelsius_converts_millidegrees()
    {
        var temperature = SystemMetricsService.ParseTemperatureCelsius("45000");

        Assert.Equal(45.0, temperature);
    }

    [Fact]
    public void ParseTemperatureCelsius_returns_null_for_missing_content()
    {
        Assert.Null(SystemMetricsService.ParseTemperatureCelsius("unavailable"));
    }

    [Fact]
    public void ParseMemInfo_reads_expected_fields()
    {
        const string memInfo =
            "MemTotal:        1000000 kB\n" +
            "MemAvailable:     400000 kB\n" +
            "Cached:           100000 kB\n" +
            "SwapTotal:        200000 kB\n" +
            "SwapFree:          50000 kB\n";

        var reading = SystemMetricsService.ParseMemInfo(memInfo);

        Assert.NotNull(reading);
        Assert.Equal(1000000, reading!.Value.TotalKb);
        Assert.Equal(400000, reading.Value.AvailableKb);
        Assert.Equal(100000, reading.Value.CachedKb);
        Assert.Equal(200000, reading.Value.SwapTotalKb);
        Assert.Equal(50000, reading.Value.SwapFreeKb);
    }

    [Fact]
    public void ParseMemInfo_returns_null_when_required_fields_missing()
    {
        Assert.Null(SystemMetricsService.ParseMemInfo("SomeOtherField: 1 kB"));
    }

    [Fact]
    public void GetMemoryMetrics_computes_used_bytes_from_meminfo_reading()
    {
        var reading = new SystemMetricsService.MemInfoReading(1000, 400, 100, 200, 50);
        var usedKb = reading.TotalKb - reading.AvailableKb;

        Assert.Equal(600, usedKb);
    }
}
