using WebApp.Services;

namespace WebApp.Endpoints;

internal static class StorageEndpoints
{
    public static IEndpointRouteBuilder MapStorageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/storage/usage", GetUsage);
        return endpoints;
    }

    private static IResult GetUsage(IStorageUsageService storageUsageService) =>
        Results.Ok(storageUsageService.GetUsage());
}
