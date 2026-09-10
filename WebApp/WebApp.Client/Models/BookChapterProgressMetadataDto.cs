namespace WebApp.Client.Models;

public sealed record BookChapterProgressMetadataDto(
    string ChapterId,
    int ChapterIndex,
    int WordCount);
