using System.Globalization;
using WebApp.Client.Models;

namespace WebApp.Services;

internal sealed class NetworkMetricsService : INetworkMetricsService
{
    private const string NetDevPath = "/proc/net/dev";

    private readonly object _gate = new();
    private Snapshot? _previousSnapshot;

    public DashboardNetworkDto GetNetworkMetrics()
    {
        IReadOnlyList<InterfaceSample>? samples;
        try
        {
            samples = ParseNetDev(File.ReadAllText(NetDevPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            samples = null;
        }

        if (samples is null)
        {
            return new DashboardNetworkDto(false, null, null, 0, 0, []);
        }

        double? uploadBytesPerSecond = null;
        double? downloadBytesPerSecond = null;
        var now = DateTime.UtcNow;

        lock (_gate)
        {
            if (_previousSnapshot is not null)
            {
                var elapsedSeconds = (now - _previousSnapshot.Value.TimestampUtc).TotalSeconds;
                if (elapsedSeconds > 0)
                {
                    var previousByName = _previousSnapshot.Value.Interfaces.ToDictionary(item => item.Name);
                    long rxDelta = 0, txDelta = 0;
                    foreach (var sample in samples)
                    {
                        if (previousByName.TryGetValue(sample.Name, out var previous))
                        {
                            rxDelta += Math.Max(0, sample.RxBytes - previous.RxBytes);
                            txDelta += Math.Max(0, sample.TxBytes - previous.TxBytes);
                        }
                    }

                    downloadBytesPerSecond = rxDelta / elapsedSeconds;
                    uploadBytesPerSecond = txDelta / elapsedSeconds;
                }
            }

            _previousSnapshot = new Snapshot(now, samples);
        }

        var interfaces = samples
            .Select(sample => new DashboardNetworkInterfaceDto(sample.Name, sample.RxBytes, sample.TxBytes, sample.Errors, sample.Drops))
            .ToList();

        return new DashboardNetworkDto(
            true,
            uploadBytesPerSecond,
            downloadBytesPerSecond,
            interfaces.Sum(item => item.Errors),
            interfaces.Sum(item => item.Drops),
            interfaces);
    }

    internal readonly record struct InterfaceSample(string Name, long RxBytes, long RxErrors, long RxDrops, long TxBytes, long TxErrors, long TxDrops)
    {
        public long Errors => RxErrors + TxErrors;
        public long Drops => RxDrops + TxDrops;
    }

    private readonly record struct Snapshot(DateTime TimestampUtc, IReadOnlyList<InterfaceSample> Interfaces);

    internal static IReadOnlyList<InterfaceSample>? ParseNetDev(string netDevContent)
    {
        var lines = netDevContent.Split('\n');
        if (lines.Length < 3)
        {
            return null;
        }

        var results = new List<InterfaceSample>();
        foreach (var line in lines.Skip(2))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0)
            {
                continue;
            }

            var name = line[..separatorIndex].Trim();
            if (string.Equals(name, "lo", StringComparison.Ordinal))
            {
                continue;
            }

            var fields = line[(separatorIndex + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 16)
            {
                continue;
            }

            if (!long.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rxBytes) ||
                !long.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rxErrors) ||
                !long.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rxDrops) ||
                !long.TryParse(fields[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var txBytes) ||
                !long.TryParse(fields[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out var txErrors) ||
                !long.TryParse(fields[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out var txDrops))
            {
                continue;
            }

            results.Add(new InterfaceSample(name, rxBytes, rxErrors, rxDrops, txBytes, txErrors, txDrops));
        }

        return results;
    }
}
