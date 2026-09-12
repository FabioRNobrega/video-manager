using System.Globalization;
using Microsoft.Extensions.Options;
using WebApp.Client.Models;
using WebApp.Configuration;

namespace WebApp.Services;

public sealed class StorageUsageService(IOptions<ArchiveRootOptions> archiveRootOptions) : IStorageUsageService
{
    private const string DiskStatsPath = "/proc/diskstats";
    private const int SectorSizeBytes = 512;

    private readonly string _path = archiveRootOptions.Value.Path;
    private readonly object _gate = new();
    private (DateTime TimestampUtc, long ReadSectors, long WriteSectors)? _previousSample;

    public StorageUsageDto GetUsage()
    {
        try
        {
            var drive = new DriveInfo(_path);
            var totalBytes = drive.TotalSize;
            var usedBytes = totalBytes - drive.AvailableFreeSpace;
            return new StorageUsageDto(Math.Max(0, usedBytes), Math.Max(0, totalBytes));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new StorageUsageDto(0, 0);
        }
    }

    public (double? ReadBytesPerSecond, double? WriteBytesPerSecond) GetThroughput()
    {
        (long ReadSectors, long WriteSectors)? totals;
        try
        {
            totals = ParseDiskStats(File.ReadAllText(DiskStatsPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            totals = null;
        }

        if (totals is null)
        {
            return (null, null);
        }

        var now = DateTime.UtcNow;
        double? readBytesPerSecond = null;
        double? writeBytesPerSecond = null;

        lock (_gate)
        {
            if (_previousSample is not null)
            {
                var elapsedSeconds = (now - _previousSample.Value.TimestampUtc).TotalSeconds;
                if (elapsedSeconds > 0)
                {
                    var readDelta = Math.Max(0, totals.Value.ReadSectors - _previousSample.Value.ReadSectors);
                    var writeDelta = Math.Max(0, totals.Value.WriteSectors - _previousSample.Value.WriteSectors);
                    readBytesPerSecond = readDelta * SectorSizeBytes / elapsedSeconds;
                    writeBytesPerSecond = writeDelta * SectorSizeBytes / elapsedSeconds;
                }
            }

            _previousSample = (now, totals.Value.ReadSectors, totals.Value.WriteSectors);
        }

        return (readBytesPerSecond, writeBytesPerSecond);
    }

    internal static (long ReadSectors, long WriteSectors)? ParseDiskStats(string diskStatsContent)
    {
        long readSectors = 0, writeSectors = 0;
        var found = false;

        foreach (var line in diskStatsContent.Split('\n'))
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 10)
            {
                continue;
            }

            var deviceName = fields[2];
            if (deviceName.StartsWith("loop", StringComparison.Ordinal) ||
                deviceName.StartsWith("ram", StringComparison.Ordinal))
            {
                continue;
            }

            if (!long.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sectorsRead) ||
                !long.TryParse(fields[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sectorsWritten))
            {
                continue;
            }

            readSectors += sectorsRead;
            writeSectors += sectorsWritten;
            found = true;
        }

        return found ? (readSectors, writeSectors) : null;
    }
}
