using WebApp.Models;

namespace WebApp.Services;

internal interface ISubtitleJobQueue
{
    bool TryEnqueue(SubtitleJob job);

    Task<SubtitleJob> DequeueAsync(CancellationToken cancellationToken);

    bool IsActive(string cacheKey);

    void Release(string cacheKey);

    int ActiveCount { get; }
}
