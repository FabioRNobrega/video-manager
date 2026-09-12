using WebApp.Models;

namespace WebApp.Services;

internal interface ICutJobQueue
{
    bool TryEnqueue(CutJob job);

    Task<CutJob> DequeueAsync(CancellationToken cancellationToken);

    void Complete();

    int ActiveCount { get; }
}
