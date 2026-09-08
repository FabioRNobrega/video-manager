using WebApp.Client.Models;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Endpoints;

internal static class ArchiveEndpoints
{
    public static IEndpointRouteBuilder MapArchiveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/archive/{category}/items", List);
        endpoints.MapGet("/api/archive/{category}/items/{id}/stream", StreamVideo);
        endpoints.MapGet("/api/archive/{category}/items/{id}/audio", StreamAudio);
        endpoints.MapGet("/api/archive/{category}/items/{id}/cover", GetAlbumCover);
        endpoints.MapGet("/api/archive/{category}/items/{id}/image", GetImage);
        endpoints.MapGet("/api/archive/{category}/items/{id}/thumbnail", GetThumbnail);
        endpoints.MapGet("/api/archive/{category}/items/{id}/preview", GetPreview);
        endpoints.MapGet("/api/archive/{category}/items/{id}/subtitle", GetSubtitle);
        endpoints.MapPost("/api/archive/{category}/folders", CreateFolder);
        endpoints.MapPatch("/api/archive/{category}/items/{id}/name", Rename);
        endpoints.MapPatch("/api/archive/{category}/items/{id}/location", Move);
        endpoints.MapDelete("/api/archive/{category}/items/{id}", MoveToTrash);
        return endpoints;
    }

    private static async Task<IResult> List(
        string category,
        string? folderId,
        IArchiveService archive,
        ThumbnailCoordinator thumbnailCoordinator,
        HoverPreviewCoordinator hoverPreviewCoordinator,
        SubtitleCoordinator subtitleCoordinator,
        VideoMetadataCoordinator metadataCoordinator,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(() => ToDtoAsync(
            archive.List(category, folderId),
            thumbnailCoordinator,
            hoverPreviewCoordinator,
            subtitleCoordinator,
            metadataCoordinator,
            cancellationToken));

    private static async Task<IResult> CreateFolder(
        string category,
        CreateFolderRequest request,
        IArchiveService archive,
        ThumbnailCoordinator thumbnailCoordinator,
        HoverPreviewCoordinator hoverPreviewCoordinator,
        SubtitleCoordinator subtitleCoordinator,
        VideoMetadataCoordinator metadataCoordinator,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(() => ToDtoAsync(
            archive.CreateFolder(category, request.ParentId, request.Name),
            thumbnailCoordinator,
            hoverPreviewCoordinator,
            subtitleCoordinator,
            metadataCoordinator,
            cancellationToken));

    private static async Task<IResult> Rename(
        string category,
        string id,
        RenameArchiveItemRequest request,
        IArchiveService archive,
        ThumbnailCoordinator thumbnailCoordinator,
        HoverPreviewCoordinator hoverPreviewCoordinator,
        SubtitleCoordinator subtitleCoordinator,
        VideoMetadataCoordinator metadataCoordinator,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(() => ToDtoAsync(
            archive.Rename(category, id, request.Name),
            thumbnailCoordinator,
            hoverPreviewCoordinator,
            subtitleCoordinator,
            metadataCoordinator,
            cancellationToken));

    private static async Task<IResult> Move(
        string category,
        string id,
        MoveArchiveItemRequest request,
        IArchiveService archive,
        ThumbnailCoordinator thumbnailCoordinator,
        HoverPreviewCoordinator hoverPreviewCoordinator,
        SubtitleCoordinator subtitleCoordinator,
        VideoMetadataCoordinator metadataCoordinator,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(() => ToDtoAsync(
            archive.Move(category, id, request.DestinationFolderId),
            thumbnailCoordinator,
            hoverPreviewCoordinator,
            subtitleCoordinator,
            metadataCoordinator,
            cancellationToken));

    private static async Task<IResult> MoveToTrash(
        string category,
        string id,
        IArchiveService archive,
        ThumbnailCoordinator thumbnailCoordinator,
        HoverPreviewCoordinator hoverPreviewCoordinator,
        SubtitleCoordinator subtitleCoordinator,
        VideoMetadataCoordinator metadataCoordinator,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(() => ToDtoAsync(
            archive.MoveToTrash(category, id),
            thumbnailCoordinator,
            hoverPreviewCoordinator,
            subtitleCoordinator,
            metadataCoordinator,
            cancellationToken));

    private static IResult StreamVideo(string category, string id, IArchiveService archive)
    {
        if (!archive.TryResolveVideo(category, id, out var item) || item is null || item.Extension is null)
        {
            return Results.NotFound();
        }

        if (!VideoContentTypes.TryGetValue(item.Extension, out var contentType))
        {
            return Results.NotFound();
        }

        try
        {
            var stream = new FileStream(
                item.PhysicalPath,
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

    private static IResult StreamAudio(string category, string id, IArchiveService archive)
    {
        if (!archive.TryResolveMusic(category, id, out var item) || item is null || item.Extension is null)
        {
            return Results.NotFound();
        }

        if (!AudioContentTypes.TryGetValue(item.Extension, out var contentType))
        {
            return Results.NotFound();
        }

        try
        {
            var stream = new FileStream(
                item.PhysicalPath,
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

    private static IResult GetAlbumCover(string category, string id, IArchiveService archive)
    {
        if (!archive.TryResolveAlbumCover(category, id, out var cover) || cover is null)
        {
            return Results.NotFound();
        }

        if (!ImageContentTypes.TryGetValue(cover.Extension, out var contentType))
        {
            return Results.NotFound();
        }

        return Results.File(cover.PhysicalPath, contentType, lastModified: cover.LastWriteTimeUtc);
    }

    private static IResult GetImage(string category, string id, IArchiveService archive)
    {
        if (!archive.TryResolveImage(category, id, out var item) || item is null || item.Extension is null)
        {
            return Results.NotFound();
        }

        if (!ImageContentTypes.TryGetValue(item.Extension, out var contentType))
        {
            return Results.NotFound();
        }

        return Results.File(item.PhysicalPath, contentType, lastModified: item.LastWriteTimeUtc);
    }

    private static IResult GetThumbnail(
        string category,
        string id,
        IArchiveService archive,
        ThumbnailCoordinator coordinator)
    {
        if (!TryResolveMediaEntry(category, id, archive, out var entry) ||
            entry is null ||
            coordinator.Resolve(entry) != ThumbnailState.Ready)
        {
            return Results.NotFound();
        }

        return Results.File(coordinator.GetFinalPath(entry), "image/jpeg");
    }

    private static IResult GetPreview(
        string category,
        string id,
        IArchiveService archive,
        HoverPreviewCoordinator coordinator)
    {
        if (!TryResolveMediaEntry(category, id, archive, out var entry) ||
            entry is null ||
            coordinator.Resolve(entry) != HoverPreviewState.Ready)
        {
            return Results.NotFound();
        }

        return Results.File(coordinator.GetFinalPath(entry), "video/mp4", enableRangeProcessing: true);
    }

    private static IResult GetSubtitle(
        string category,
        string id,
        IArchiveService archive,
        SubtitleCoordinator coordinator)
    {
        if (!TryResolveMediaEntry(category, id, archive, out var entry) ||
            entry is null ||
            coordinator.Resolve(entry) != SubtitleState.Ready)
        {
            return Results.NotFound();
        }

        return Results.File(coordinator.GetFinalPath(entry), "text/vtt");
    }

    private static IResult Execute(Func<ArchiveListingDto> action)
    {
        try
        {
            return Results.Ok(action());
        }
        catch (ArchiveValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (ArchiveForbiddenException exception)
        {
            return Results.Problem(title: "Archive operation is not allowed.", detail: exception.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ArchiveNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArchiveConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Results.Problem(title: "Archive operation failed.", detail: "The archive item could not be changed.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<ArchiveListingDto>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (ArchiveValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (ArchiveForbiddenException exception)
        {
            return Results.Problem(title: "Archive operation is not allowed.", detail: exception.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ArchiveNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArchiveConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Results.Problem(title: "Archive operation failed.", detail: "The archive item could not be changed.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static readonly IReadOnlyDictionary<string, string> VideoContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".mp4"] = "video/mp4",
            [".webm"] = "video/webm",
            [".mov"] = "video/quicktime",
            [".m4v"] = "video/x-m4v"
        };

    private static readonly IReadOnlyDictionary<string, string> AudioContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".mp3"] = "audio/mpeg",
            [".wav"] = "audio/wav",
            [".m4a"] = "audio/mp4"
        };

    private static readonly IReadOnlyDictionary<string, string> ImageContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg"
        };

    private static ArchiveListingDto ToDto(ArchiveListing listing) =>
        new(
            listing.Category.Key,
            listing.Category.DisplayName,
            IsCategoryRoot(listing) ? null : listing.CurrentFolder.Id,
            listing.ParentFolder?.Id,
            listing.Category.CanCreateFolder,
            listing.Breadcrumbs,
            listing.Items.Select(item => ToDto(item)).ToList());

    private static ArchiveListingDto ToDto(
        ArchiveListing listing,
        ThumbnailCoordinator thumbnailCoordinator,
        HoverPreviewCoordinator hoverPreviewCoordinator,
        SubtitleCoordinator subtitleCoordinator)
    {
        var videoEntries = listing.Items
            .Where(item => item.IsVideo)
            .Select(ToMediaEntry)
            .ToList();
        thumbnailCoordinator.Reconcile(videoEntries);
        hoverPreviewCoordinator.Reconcile(videoEntries);
        subtitleCoordinator.Reconcile(videoEntries);

        return new(
            listing.Category.Key,
            listing.Category.DisplayName,
            IsCategoryRoot(listing) ? null : listing.CurrentFolder.Id,
            listing.ParentFolder?.Id,
            listing.Category.CanCreateFolder,
            listing.Breadcrumbs,
            listing.Items.Select(item => ToDto(item, thumbnailCoordinator, hoverPreviewCoordinator, subtitleCoordinator)).ToList());
    }

    private static async Task<ArchiveListingDto> ToDtoAsync(
        ArchiveListing listing,
        ThumbnailCoordinator thumbnailCoordinator,
        HoverPreviewCoordinator hoverPreviewCoordinator,
        SubtitleCoordinator subtitleCoordinator,
        VideoMetadataCoordinator metadataCoordinator,
        CancellationToken cancellationToken)
    {
        var videoEntries = listing.Items
            .Where(item => item.IsVideo)
            .Select(ToMediaEntry)
            .ToList();
        thumbnailCoordinator.Reconcile(videoEntries);
        hoverPreviewCoordinator.Reconcile(videoEntries);
        subtitleCoordinator.Reconcile(videoEntries);

        var items = await Task.WhenAll(listing.Items.Select(item =>
            ToDtoAsync(item, thumbnailCoordinator, hoverPreviewCoordinator, subtitleCoordinator, metadataCoordinator, cancellationToken)));

        return new(
            listing.Category.Key,
            listing.Category.DisplayName,
            IsCategoryRoot(listing) ? null : listing.CurrentFolder.Id,
            listing.ParentFolder?.Id,
            listing.Category.CanCreateFolder,
            listing.Breadcrumbs,
            items);
    }

    private static ArchiveItemDto ToDto(ArchiveItemEntry item) =>
        new(
            item.Id,
            item.Name,
            item.Kind,
            item.Extension,
            item.SizeBytes,
            item.LastWriteTimeUtc,
            item.IsVideo,
            IsMusic: item.IsMusic,
            AudioUrl: AudioUrl(item),
            AlbumCoverUrl: AlbumCoverUrl(item),
            IsImage: item.IsImage,
            ImageUrl: ImageUrl(item));

    private static ArchiveItemDto ToDto(
        ArchiveItemEntry item,
        ThumbnailCoordinator thumbnailCoordinator,
        HoverPreviewCoordinator hoverPreviewCoordinator,
        SubtitleCoordinator subtitleCoordinator)
    {
        if (!item.IsVideo && !item.IsMusic)
        {
            return ToDto(item);
        }

        var entry = ToMediaEntry(item);
        var thumbnailState = ThumbnailState.Unavailable;
        string? thumbnailUrl = null;
        var hoverPreviewState = HoverPreviewState.Unavailable;
        string? hoverPreviewUrl = null;
        var subtitleState = SubtitleState.Unavailable;
        string? subtitleUrl = null;
        if (item.IsVideo)
        {
            thumbnailState = thumbnailCoordinator.Resolve(entry);
            thumbnailUrl = thumbnailState == ThumbnailState.Ready
                ? $"/api/archive/{Uri.EscapeDataString(item.Category.Key)}/items/{Uri.EscapeDataString(item.Id)}/thumbnail"
                : null;
            hoverPreviewState = hoverPreviewCoordinator.Resolve(entry);
            hoverPreviewUrl = hoverPreviewState == HoverPreviewState.Ready
                ? $"/api/archive/{Uri.EscapeDataString(item.Category.Key)}/items/{Uri.EscapeDataString(item.Id)}/preview"
                : null;
            subtitleState = subtitleCoordinator.Resolve(entry);
            subtitleUrl = subtitleState == SubtitleState.Ready
                ? $"/api/archive/{Uri.EscapeDataString(item.Category.Key)}/items/{Uri.EscapeDataString(item.Id)}/subtitle"
                : null;
        }

        return new ArchiveItemDto(
            item.Id,
            item.Name,
            item.Kind,
            item.Extension,
            item.SizeBytes,
            item.LastWriteTimeUtc,
            item.IsVideo,
            thumbnailState,
            thumbnailUrl,
            hoverPreviewState,
            hoverPreviewUrl,
            subtitleState,
            subtitleUrl,
            IsMusic: item.IsMusic,
            AudioUrl: AudioUrl(item),
            AlbumCoverUrl: AlbumCoverUrl(item));
    }

    private static async Task<ArchiveItemDto> ToDtoAsync(
        ArchiveItemEntry item,
        ThumbnailCoordinator thumbnailCoordinator,
        HoverPreviewCoordinator hoverPreviewCoordinator,
        SubtitleCoordinator subtitleCoordinator,
        VideoMetadataCoordinator metadataCoordinator,
        CancellationToken cancellationToken)
    {
        if (!item.IsVideo && !item.IsMusic)
        {
            return ToDto(item);
        }

        var entry = ToMediaEntry(item);
        var thumbnailState = ThumbnailState.Unavailable;
        string? thumbnailUrl = null;
        var hoverPreviewState = HoverPreviewState.Unavailable;
        string? hoverPreviewUrl = null;
        var subtitleState = SubtitleState.Unavailable;
        string? subtitleUrl = null;
        if (item.IsVideo)
        {
            thumbnailState = thumbnailCoordinator.Resolve(entry);
            thumbnailUrl = thumbnailState == ThumbnailState.Ready
                ? $"/api/archive/{Uri.EscapeDataString(item.Category.Key)}/items/{Uri.EscapeDataString(item.Id)}/thumbnail"
                : null;
            hoverPreviewState = hoverPreviewCoordinator.Resolve(entry);
            hoverPreviewUrl = hoverPreviewState == HoverPreviewState.Ready
                ? $"/api/archive/{Uri.EscapeDataString(item.Category.Key)}/items/{Uri.EscapeDataString(item.Id)}/preview"
                : null;
            subtitleState = subtitleCoordinator.Resolve(entry);
            subtitleUrl = subtitleState == SubtitleState.Ready
                ? $"/api/archive/{Uri.EscapeDataString(item.Category.Key)}/items/{Uri.EscapeDataString(item.Id)}/subtitle"
                : null;
        }

        VideoMetadata metadata;
        try
        {
            metadata = await metadataCoordinator.GetOrComputeAsync(entry, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            metadata = new VideoMetadata(null, null, null);
        }

        return new ArchiveItemDto(
            item.Id,
            item.Name,
            item.Kind,
            item.Extension,
            item.SizeBytes,
            item.LastWriteTimeUtc,
            item.IsVideo,
            thumbnailState,
            thumbnailUrl,
            hoverPreviewState,
            hoverPreviewUrl,
            subtitleState,
            subtitleUrl,
            metadata.Duration?.TotalSeconds,
            item.IsVideo ? metadata.Width : null,
            item.IsVideo ? metadata.Height : null,
            item.IsMusic,
            AudioUrl(item),
            AlbumCoverUrl(item));
    }

    private static string? AudioUrl(ArchiveItemEntry item) =>
        item.IsMusic
            ? $"/api/archive/{Uri.EscapeDataString(item.Category.Key)}/items/{Uri.EscapeDataString(item.Id)}/audio"
            : null;

    private static string? ImageUrl(ArchiveItemEntry item) =>
        item.IsImage
            ? $"/api/archive/{Uri.EscapeDataString(item.Category.Key)}/items/{Uri.EscapeDataString(item.Id)}/image"
            : null;

    private static string? AlbumCoverUrl(ArchiveItemEntry item) =>
        item.IsMusic && !string.IsNullOrWhiteSpace(item.AlbumCoverId)
            ? $"/api/archive/{Uri.EscapeDataString(item.Category.Key)}/items/{Uri.EscapeDataString(item.AlbumCoverId)}/cover"
            : null;

    private static bool TryResolveMediaEntry(
        string category,
        string id,
        IArchiveService archive,
        out VideoFileEntry? entry)
    {
        entry = null;
        if (!archive.TryResolveVideo(category, id, out var item) || item is null)
        {
            return false;
        }

        entry = ToMediaEntry(item);
        return true;
    }

    private static VideoFileEntry ToMediaEntry(ArchiveItemEntry item) =>
        new(
            item.Id,
            item.PhysicalPath,
            $"archive/{item.Category.Key}/{item.Id}/{item.Name}",
            item.Name,
            item.Extension ?? string.Empty,
            item.SizeBytes ?? 0,
            item.LastWriteTimeUtc);

    private static bool IsCategoryRoot(ArchiveListing listing) =>
        listing.ParentFolder is null;
}
