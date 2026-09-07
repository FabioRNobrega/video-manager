namespace WebApp.Models;

internal sealed record ArchiveListing(
    ArchiveCategory Category,
    ArchiveItemEntry CurrentFolder,
    ArchiveItemEntry? ParentFolder,
    IReadOnlyList<string> Breadcrumbs,
    IReadOnlyList<ArchiveItemEntry> Items);
