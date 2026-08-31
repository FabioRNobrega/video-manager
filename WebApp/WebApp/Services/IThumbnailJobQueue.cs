using WebApp.Models;

namespace WebApp.Services;

internal interface IThumbnailJobQueue
{
    bool TryEnqueue(ThumbnailJob job);

    Task<ThumbnailJob> DequeueAsync(CancellationToken cancellationToken);

    bool IsActive(string cacheKey);

    void Release(string cacheKey);
}
