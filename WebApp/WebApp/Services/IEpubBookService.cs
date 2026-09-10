using WebApp.Client.Models;
using WebApp.Models;

namespace WebApp.Services;

internal interface IEpubBookService
{
    /// <summary>
    /// Reads book metadata (title, author, cover availability, and nested table of contents) from a resolved
    /// book archive item. Returns <c>false</c> for missing, malformed, or unreadable EPUB files.
    /// </summary>
    bool TryGetBook(ArchiveItemEntry item, out BookDto? book);

    /// <summary>
    /// Reads and sanitizes a single reading-order chapter's HTML content by its opaque chapter id.
    /// </summary>
    bool TryGetChapter(ArchiveItemEntry item, string chapterId, out BookChapterDto? chapter);

    /// <summary>
    /// Reads the embedded cover image bytes and a best-effort content type, if the book has a cover.
    /// </summary>
    bool TryGetCover(ArchiveItemEntry item, out byte[]? coverBytes, out string? contentType);
}
