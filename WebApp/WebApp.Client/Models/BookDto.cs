namespace WebApp.Client.Models;

public sealed record BookDto(
    string Id,
    string Title,
    string? Author,
    bool HasCover,
    string? CoverUrl,
    IReadOnlyList<BookNavigationItemDto> Navigation,
    IReadOnlyList<string> ChapterIds,
    BookProgressDto? Progress);
