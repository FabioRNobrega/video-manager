namespace WebApp.Client.Models;

public sealed record BookChapterDto(
    string ChapterId,
    int ChapterIndex,
    string? Title,
    string ContentHtml,
    string? PreviousChapterId,
    string? NextChapterId);
