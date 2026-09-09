namespace WebApp.Client.Models;

public sealed record BookChapterDto(
    string ChapterId,
    int ChapterIndex,
    string? Title,
    string ContentHtml,
    string NormalizedText,
    int WordCount,
    string? PreviousChapterId,
    string? NextChapterId);

public sealed record BookHighlightDto(
    string Id,
    string ChapterId,
    int TextOffsetStart,
    int TextOffsetEnd,
    string SelectedText,
    string ContextBefore,
    string ContextAfter,
    DateTimeOffset SavedAt);
