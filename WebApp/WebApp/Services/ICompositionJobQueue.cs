using WebApp.Models;

namespace WebApp.Services;

internal interface ICompositionJobQueue
{
    bool TryEnqueue(CompositionJob job);

    Task<CompositionJob> DequeueAsync(CancellationToken cancellationToken);
}
