using WebApp.Client.Models;

namespace WebApp.Models;

internal sealed record ArchiveListing(
    ArchiveCategory Category,
    ArchiveItemEntry CurrentFolder,
    ArchiveItemEntry? ParentFolder,
    IReadOnlyList<ArchiveBreadcrumbDto> Breadcrumbs,
    IReadOnlyList<ArchiveItemEntry> Items);
