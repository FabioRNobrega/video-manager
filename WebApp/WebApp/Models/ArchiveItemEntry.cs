using WebApp.Client.Models;

namespace WebApp.Models;

internal sealed record ArchiveItemEntry(
    string Id,
    ArchiveCategory Category,
    string PhysicalPath,
    string Name,
    ArchiveItemKind Kind,
    string? Extension,
    long? SizeBytes,
    DateTime LastWriteTimeUtc,
    bool IsVideo);
