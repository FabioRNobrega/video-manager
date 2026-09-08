namespace WebApp.Models;

internal sealed record ArchiveAlbumCoverInfo(
    string FolderId,
    string PhysicalPath,
    string Name,
    string Extension,
    DateTime LastWriteTimeUtc);
