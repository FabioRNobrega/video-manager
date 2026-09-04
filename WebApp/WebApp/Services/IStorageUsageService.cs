using WebApp.Client.Models;

namespace WebApp.Services;

public interface IStorageUsageService
{
    StorageUsageDto GetUsage();
}
