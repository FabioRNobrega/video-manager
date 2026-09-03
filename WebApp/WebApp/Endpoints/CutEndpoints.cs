using WebApp.Client.Models;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Endpoints;

internal static class CutEndpoints
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".mp4"] = "video/mp4",
            [".webm"] = "video/webm",
            [".mov"] = "video/quicktime",
            [".m4v"] = "video/x-m4v"
        };

    public static IEndpointRouteBuilder MapCutEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/cuts", GetCurrentSnapshot);
        endpoints.MapGet("/api/cuts/{id}/stream", StreamAsync);
        endpoints.MapGet("/api/cuts/{id}/thumbnail", GetThumbnail);
        endpoints.MapGet("/api/cuts/{id}/preview", GetPreview);
        return endpoints;
    }

    private static async Task<IResult> GetCurrentSnapshot(
        IVideoCutService cuts,
        ThumbnailCoordinator thumbnailCoordinator,
        HoverPreviewCoordinator hoverPreviewCoordinator,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = await cuts.ScanAsync(cancellationToken);
            thumbnailCoordinator.Reconcile(entries);
            hoverPreviewCoordinator.Reconcile(entries);
            return Results.Ok(entries.Select(entry => BuildDto(entry, thumbnailCoordinator, hoverPreviewCoordinator)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch
        {
            return Results.Problem(
                title: "Video cuts scan failed.",
                detail: "The configured cuts folder could not be scanned.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static IResult StreamAsync(string id, IVideoCutService cuts)
    {
        if (!cuts.TryResolve(id, out var entry) || entry is null)
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

    private static IResult GetThumbnail(string id, IVideoCutService cuts, ThumbnailCoordinator coordinator)
    {
        if (!cuts.TryResolve(id, out var entry) || entry is null)
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

    private static IResult GetPreview(string id, IVideoCutService cuts, HoverPreviewCoordinator coordinator)
    {
        if (!cuts.TryResolve(id, out var entry) || entry is null)
        {
            return Results.NotFound();
        }

        if (coordinator.Resolve(entry) != HoverPreviewState.Ready)
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

            return Results.Stream(stream, "video/mp4", enableRangeProcessing: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static VideoItemDto BuildDto(
        VideoFileEntry entry, ThumbnailCoordinator thumbnailCoordinator, HoverPreviewCoordinator hoverPreviewCoordinator)
    {
        var thumbnailState = thumbnailCoordinator.Resolve(entry);
        var thumbnailUrl = thumbnailState == ThumbnailState.Ready ? $"/api/cuts/{entry.Id}/thumbnail" : null;
        var hoverPreviewState = hoverPreviewCoordinator.Resolve(entry);
        var hoverPreviewUrl = hoverPreviewState == HoverPreviewState.Ready ? $"/api/cuts/{entry.Id}/preview" : null;
        return new VideoItemDto(
            entry.Id, entry.Name, entry.Extension, entry.SizeBytes,
            thumbnailState, thumbnailUrl, hoverPreviewState, hoverPreviewUrl);
    }
}
