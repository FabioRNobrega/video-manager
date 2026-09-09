namespace WebApp.Client.Models;

public sealed record BookChapterDto(
    string ChapterId,
    int ChapterIndex,
    string? Title,
    string ContentHtml,
    int WordCount,
    string? PreviousChapterId,
    string? NextChapterId);
