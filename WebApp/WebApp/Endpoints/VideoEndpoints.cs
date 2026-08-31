using WebApp.Client.Models;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Endpoints;

internal static class VideoEndpoints
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".mp4"] = "video/mp4",
            [".webm"] = "video/webm",
            [".mov"] = "video/quicktime",
            [".m4v"] = "video/x-m4v"
        };

    public static IEndpointRouteBuilder MapVideoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/videos/scan", ScanAsync);
        endpoints.MapGet("/api/videos", GetCurrentSnapshot);
        endpoints.MapGet("/api/videos/{id}/stream", StreamAsync);
        endpoints.MapGet("/api/videos/{id}/thumbnail", GetThumbnail);
        return endpoints;
    }

    private static async Task<IResult> ScanAsync(
        IVideoLibraryService library,
        ThumbnailCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = await library.ScanAsync(cancellationToken);
            return Results.Ok(entries.Select(entry => BuildDto(entry, coordinator)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch
        {
            return Results.Problem(
                title: "Video library scan failed.",
                detail: "The configured library could not be scanned.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static IResult GetCurrentSnapshot(IVideoLibraryService library, ThumbnailCoordinator coordinator)
    {
        var entries = library.GetCurrentSnapshot();
        coordinator.Reconcile(entries);
        return Results.Ok(entries.Select(entry => BuildDto(entry, coordinator)));
    }

    private static IResult StreamAsync(string id, IVideoLibraryService library)
    {
        if (!library.TryResolve(id, out var entry) || entry is null)
        {
            return Results.NotFound();
        }

        try
        {
            if (!ContentTypes.TryGetValue(entry.Extension, out var contentType))
            {
                return Results.NotFound();
            }

            var stream = new FileStream(
                entry.PhysicalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            return Results.Stream(stream, contentType, enableRangeProcessing: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static IResult GetThumbnail(string id, IVideoLibraryService library, ThumbnailCoordinator coordinator)
    {
        if (!library.TryResolve(id, out var entry) || entry is null)
        {
            return Results.NotFound();
        }

        if (coordinator.Resolve(entry) != ThumbnailState.Ready)
        {
            return Results.NotFound();
        }

        try
        {
            var stream = new FileStream(
                coordinator.GetFinalPath(entry),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            return Results.Stream(stream, "image/jpeg");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static VideoItemDto BuildDto(VideoFileEntry entry, ThumbnailCoordinator coordinator)
    {
        var state = coordinator.Resolve(entry);
        var thumbnailUrl = state == ThumbnailState.Ready ? $"/api/videos/{entry.Id}/thumbnail" : null;
        return new VideoItemDto(entry.Id, entry.Name, entry.Extension, entry.SizeBytes, state, thumbnailUrl);
    }
}
