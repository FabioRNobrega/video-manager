namespace WebApp.Models;

internal sealed record ArchiveCategory(
    string Key,
    string DisplayName,
    string FolderName,
    string Icon,
    bool CanCreateFolder)
{
    public static readonly IReadOnlyList<ArchiveCategory> Defaults =
    [
        new("videos", "Videos", "Videos", "bi-collection-play", true),
        new("photos", "Photos", "Pictures", "bi-image", true),
        new("music", "Music", "Music", "bi-music-note-beamed", true),
        new("documents", "Documents", "Documents", "bi-file-earmark-text", true),
        new("books", "Books", "Books", "bi-journal-bookmark-fill", true),
        new("downloads", "Downloads", "Downloads", "bi-download", true),
        new("shared", "Shared", "Shared", "bi-people", true),
        new("family", "Family", "Family", "bi-house-heart", true),
        new("history", "History", "History", "bi-clock-history", false),
        new("trash", "Trash", "Trash", "bi-trash3", false)
    ];

    public static bool TryGet(string key, out ArchiveCategory? category)
    {
        category = Defaults.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        return category is not null;
    }
}
