using Microsoft.Extensions.Options;
using WebApp.Client.Models;
using WebApp.Configuration;

namespace WebApp.Services;

public sealed class StorageUsageService(IOptions<VideoLibraryOptions> videoLibraryOptions) : IStorageUsageService
{
    private readonly string _path = videoLibraryOptions.Value.Path;

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
}
