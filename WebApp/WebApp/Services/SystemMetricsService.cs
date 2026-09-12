using System.Globalization;
using WebApp.Client.Models;

namespace WebApp.Services;

internal sealed class SystemMetricsService : ISystemMetricsService
{
    private const string StatPath = "/proc/stat";
    private const string LoadAveragePath = "/proc/loadavg";
    private const string UptimePath = "/proc/uptime";
    private const string MemInfoPath = "/proc/meminfo";
    private const string ThermalZonePath = "/sys/class/thermal/thermal_zone0/temp";

    private readonly object _gate = new();
    private CpuStatSample? _previousCpuSample;

    public DashboardSystemDto GetSystemMetrics()
    {
        double? cpuPercent = null;
        try
        {
            var statContent = File.ReadAllText(StatPath);
            var sample = ParseCpuStatSample(statContent);
            if (sample is not null)
            {
                lock (_gate)
                {
                    if (_previousCpuSample is not null)
                    {
                        cpuPercent = ComputeCpuPercent(_previousCpuSample.Value, sample.Value);
                    }

                    _previousCpuSample = sample;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // CPU percent stays unavailable.
        }

        (double, double, double)? loadAverage = null;
        try
        {
            loadAverage = ParseLoadAverage(File.ReadAllText(LoadAveragePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Load average stays unavailable.
        }

        double? uptimeSeconds = null;
        try
        {
            uptimeSeconds = ParseUptimeSeconds(File.ReadAllText(UptimePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Uptime stays unavailable.
        }

        double? temperatureCelsius = null;
        try
        {
            temperatureCelsius = ParseTemperatureCelsius(File.ReadAllText(ThermalZonePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Temperature stays unavailable.
        }

        var isAvailable = loadAverage is not null || uptimeSeconds is not null || cpuPercent is not null;

        return new DashboardSystemDto(
            isAvailable,
            cpuPercent,
            loadAverage?.Item1,
            loadAverage?.Item2,
            loadAverage?.Item3,
            temperatureCelsius,
            uptimeSeconds);
    }

    public DashboardMemoryDto GetMemoryMetrics()
    {
        try
        {
            var reading = ParseMemInfo(File.ReadAllText(MemInfoPath));
            if (reading is null)
            {
                return new DashboardMemoryDto(false, null, null, null, null, null, null);
            }

            var value = reading.Value;
            var usedBytes = Math.Max(0, value.TotalKb - value.AvailableKb) * 1024L;
            var swapUsedBytes = Math.Max(0, value.SwapTotalKb - value.SwapFreeKb) * 1024L;

            return new DashboardMemoryDto(
                true,
                usedBytes,
                value.TotalKb * 1024L,
                value.AvailableKb * 1024L,
                value.CachedKb * 1024L,
                swapUsedBytes,
                value.SwapTotalKb * 1024L);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new DashboardMemoryDto(false, null, null, null, null, null, null);
        }
    }

    internal readonly record struct CpuStatSample(long IdleTicks, long TotalTicks);

    internal readonly record struct MemInfoReading(
        long TotalKb, long AvailableKb, long CachedKb, long SwapTotalKb, long SwapFreeKb);

    internal static CpuStatSample? ParseCpuStatSample(string statContent)
    {
        foreach (var line in statContent.Split('\n'))
        {
            if (!line.StartsWith("cpu ", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 8)
            {
                return null;
            }

            var values = new long[parts.Length - 1];
            for (var i = 1; i < parts.Length; i++)
            {
                if (!long.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i - 1]))
                {
                    return null;
                }
            }

            var idle = values[3] + values[4]; // idle + iowait
            var total = values.Sum();
            return new CpuStatSample(idle, total);
        }

        return null;
    }

    internal static double? ComputeCpuPercent(CpuStatSample previous, CpuStatSample current)
    {
        var totalDelta = current.TotalTicks - previous.TotalTicks;
        if (totalDelta <= 0)
        {
            return null;
        }

        var idleDelta = current.IdleTicks - previous.IdleTicks;
        var usedDelta = totalDelta - idleDelta;
        return Math.Clamp(usedDelta * 100.0 / totalDelta, 0, 100);
    }

    internal static (double, double, double)? ParseLoadAverage(string loadAverageContent)
    {
        var parts = loadAverageContent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var one) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var five) &&
            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var fifteen))
        {
            return (one, five, fifteen);
        }

        return null;
    }

    internal static double? ParseUptimeSeconds(string uptimeContent)
    {
        var parts = uptimeContent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1)
        {
            return null;
        }

        return double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : null;
    }

    internal static double? ParseTemperatureCelsius(string thermalContent)
    {
        var trimmed = thermalContent.Trim();
        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliCelsius)
            ? milliCelsius / 1000.0
            : null;
    }

    internal static MemInfoReading? ParseMemInfo(string memInfoContent)
    {
        long? total = null, available = null, cached = null, swapTotal = null, swapFree = null;

        foreach (var line in memInfoContent.Split('\n'))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var valuePart = line[(separatorIndex + 1)..].Trim();
            var valueToken = valuePart.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (valueToken is null || !long.TryParse(valueToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
            {
                continue;
            }

            switch (key)
            {
                case "MemTotal":
                    total = kb;
                    break;
                case "MemAvailable":
                    available = kb;
                    break;
                case "Cached":
                    cached = kb;
                    break;
                case "SwapTotal":
                    swapTotal = kb;
                    break;
                case "SwapFree":
                    swapFree = kb;
                    break;
            }
        }

        if (total is null || available is null)
        {
            return null;
        }

        return new MemInfoReading(total.Value, available.Value, cached ?? 0, swapTotal ?? 0, swapFree ?? 0);
    }
}
