namespace WebApp.Client.Models;

public sealed record ArchiveListingDto(
    string Category,
    string DisplayName,
    string? CurrentFolderId,
    string? ParentFolderId,
    bool CanCreateFolder,
    IReadOnlyList<ArchiveBreadcrumbDto> Breadcrumbs,
    IReadOnlyList<ArchiveItemDto> Items);
