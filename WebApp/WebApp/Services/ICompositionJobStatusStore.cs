using WebApp.Models;

namespace WebApp.Services;

internal interface ICompositionJobStatusStore
{
    void Seed(string jobId);

    void MarkProcessing(string jobId);

    void MarkCompleted(string jobId, string resultVideoId);

    void MarkFailed(string jobId, string? diagnostic);

    IReadOnlyList<CompositionJobStatus> GetAll();
}
