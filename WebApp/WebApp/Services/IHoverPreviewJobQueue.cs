using WebApp.Models;

namespace WebApp.Services;

internal interface IHoverPreviewJobQueue
{
    bool TryEnqueue(HoverPreviewJob job);

    Task<HoverPreviewJob> DequeueAsync(CancellationToken cancellationToken);

    bool IsActive(string cacheKey);

    void Release(string cacheKey);
}
