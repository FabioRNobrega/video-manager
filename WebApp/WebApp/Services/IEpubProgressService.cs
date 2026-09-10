using WebApp.Client.Models;

namespace WebApp.Services;

internal interface IEpubProgressService
{
    /// <summary>
    /// Loads previously saved chapter/position progress for a book, keyed by a stable private hash of category,
    /// item id, size, and last-write time. Returns <c>null</c> when no progress is saved or the stored data is
    /// missing/malformed.
    /// </summary>
    Task<BookProgressDto?> LoadProgressAsync(
        string categoryKey, string itemId, long? sizeBytes, DateTime lastWriteTimeUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Saves chapter/position progress for a book using an atomic temp-file-then-move write.
    /// </summary>
    Task SaveProgressAsync(
        string categoryKey,
        string itemId,
        long? sizeBytes,
        DateTime lastWriteTimeUtc,
        BookProgressDto progress,
        CancellationToken cancellationToken);
}
