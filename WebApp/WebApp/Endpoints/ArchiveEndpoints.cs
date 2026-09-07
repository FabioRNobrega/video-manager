using WebApp.Client.Models;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Endpoints;

internal static class ArchiveEndpoints
{
    public static IEndpointRouteBuilder MapArchiveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/archive/{category}/items", List);
        endpoints.MapPost("/api/archive/{category}/folders", CreateFolder);
        endpoints.MapPatch("/api/archive/{category}/items/{id}/name", Rename);
        endpoints.MapPatch("/api/archive/{category}/items/{id}/location", Move);
        endpoints.MapDelete("/api/archive/{category}/items/{id}", MoveToTrash);
        return endpoints;
    }

    private static IResult List(string category, string? folderId, IArchiveService archive) =>
        Execute(() => ToDto(archive.List(category, folderId)));

    private static IResult CreateFolder(
        string category,
        CreateFolderRequest request,
        IArchiveService archive) =>
        Execute(() => ToDto(archive.CreateFolder(category, request.ParentId, request.Name)));

    private static IResult Rename(
        string category,
        string id,
        RenameArchiveItemRequest request,
        IArchiveService archive) =>
        Execute(() => ToDto(archive.Rename(category, id, request.Name)));

    private static IResult Move(
        string category,
        string id,
        MoveArchiveItemRequest request,
        IArchiveService archive) =>
        Execute(() => ToDto(archive.Move(category, id, request.DestinationFolderId)));

    private static IResult MoveToTrash(string category, string id, IArchiveService archive) =>
        Execute(() => ToDto(archive.MoveToTrash(category, id)));

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

    private static ArchiveListingDto ToDto(ArchiveListing listing) =>
        new(
            listing.Category.Key,
            listing.Category.DisplayName,
            IsCategoryRoot(listing) ? null : listing.CurrentFolder.Id,
            listing.ParentFolder?.Id,
            listing.Category.CanCreateFolder,
            listing.Breadcrumbs,
            listing.Items.Select(ToDto).ToList());

    private static ArchiveItemDto ToDto(ArchiveItemEntry item) =>
        new(
            item.Id,
            item.Name,
            item.Kind,
            item.Extension,
            item.SizeBytes,
            item.LastWriteTimeUtc,
            item.IsVideo);

    private static bool IsCategoryRoot(ArchiveListing listing) =>
        listing.ParentFolder is null;
}
