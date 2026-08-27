using WebApp.Client.Models;
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
        endpoints.MapGet("/api/videos/{id}/stream", StreamAsync);
        return endpoints;
    }

    private static async Task<IResult> ScanAsync(
        IVideoLibraryService library,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = await library.ScanAsync(cancellationToken);
            var response = entries.Select(entry => new VideoItemDto(
                entry.Id,
                entry.Name,
                entry.Extension,
                entry.SizeBytes));
            return Results.Ok(response);
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
}
