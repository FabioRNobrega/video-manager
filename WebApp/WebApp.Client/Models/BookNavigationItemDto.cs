namespace WebApp.Client.Models;

public sealed record BookNavigationItemDto(
    string Title,
    string? ChapterId,
    IReadOnlyList<BookNavigationItemDto> Children);
