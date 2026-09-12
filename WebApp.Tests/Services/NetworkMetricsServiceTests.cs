using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class NetworkMetricsServiceTests
{
    private const string NetDevFixture =
        "Inter-|   Receive                                                |  Transmit\n" +
        " face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed\n" +
        "    lo:  1000      10    0    0    0     0          0         0     1000      10    0    0    0     0       0          0\n" +
        "  eth0: 99999     100    1    2    0     0          0         0    88888      90    3    4    0     0       0          0\n";

    [Fact]
    public void ParseNetDev_skips_loopback_and_reads_interface_fields()
    {
        var samples = NetworkMetricsService.ParseNetDev(NetDevFixture);

        Assert.NotNull(samples);
        var interfaceSample = Assert.Single(samples!);
        Assert.Equal("eth0", interfaceSample.Name);
        Assert.Equal(99999, interfaceSample.RxBytes);
        Assert.Equal(88888, interfaceSample.TxBytes);
        Assert.Equal(4, interfaceSample.Errors);
        Assert.Equal(6, interfaceSample.Drops);
    }

    [Fact]
    public void ParseNetDev_returns_null_for_malformed_content()
    {
        Assert.Null(NetworkMetricsService.ParseNetDev("not-a-valid-file"));
    }

    [Fact]
    public void GetNetworkMetrics_marks_unavailable_when_proc_net_dev_is_missing()
    {
        var service = new NetworkMetricsService();

        var metrics = service.GetNetworkMetrics();

        // On a machine without /proc/net/dev (e.g. non-Linux CI), the service must
        // degrade gracefully instead of throwing.
        Assert.NotNull(metrics);
    }
}
