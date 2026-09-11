using WebApp.Models;

namespace WebApp.Services;

internal interface IArchiveService
{
    ArchiveListing List(string categoryKey, string? folderId);

    ArchiveListing ListPlaylist(string categoryKey, string? folderId);

    ArchiveListing CreateFolder(string categoryKey, string? parentId, string name);

    ArchiveListing CreateFile(string categoryKey, string? parentId, string name, string extension);

    Task<ArchiveListing> SaveUploadedFileAsync(string categoryKey, string? parentId, string fileName, Stream content, CancellationToken cancellationToken);

    ArchiveListing Rename(string categoryKey, string itemId, string name);

    ArchiveListing Move(string categoryKey, string itemId, string? destinationFolderId);

    ArchiveListing MoveToTrash(string categoryKey, string itemId);

    bool TryResolveVideo(string categoryKey, string itemId, out ArchiveItemEntry? item);

    bool TryResolveMusic(string categoryKey, string itemId, out ArchiveItemEntry? item);

    bool TryResolveImage(string categoryKey, string itemId, out ArchiveItemEntry? item);

    bool TryResolveBook(string categoryKey, string itemId, out ArchiveItemEntry? item);

    bool TryResolveTextDocument(string categoryKey, string itemId, out ArchiveItemEntry? item);

    bool TryResolvePdfDocument(string categoryKey, string itemId, out ArchiveItemEntry? item);

    bool TryResolveAlbumCover(string categoryKey, string folderId, out ArchiveAlbumCoverInfo? cover);

    string GetCategoryRootPath(string categoryKey);

    string ComputeItemId(string categoryKey, string physicalPath);
}
