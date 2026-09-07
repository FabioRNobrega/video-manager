namespace WebApp.Client.Models;

public sealed record ArchiveListingDto(
    string Category,
    string DisplayName,
    string? CurrentFolderId,
    string? ParentFolderId,
    bool CanCreateFolder,
    IReadOnlyList<string> Breadcrumbs,
    IReadOnlyList<ArchiveItemDto> Items);
