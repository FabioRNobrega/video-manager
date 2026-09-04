using System.Collections.Concurrent;
using WebApp.Client.Models;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class CompositionJobStatusStore : ICompositionJobStatusStore
{
    private readonly ConcurrentDictionary<string, CompositionJobStatus> _jobs = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _order = new();

    public void Seed(string jobId)
    {
        _jobs[jobId] = new CompositionJobStatus(jobId, CompositionJobState.Pending);
        _order.Enqueue(jobId);
    }

    public void MarkProcessing(string jobId) =>
        _jobs.AddOrUpdate(
            jobId,
            _ => new CompositionJobStatus(jobId, CompositionJobState.Processing),
            (_, existing) => existing with { State = CompositionJobState.Processing });

    public void MarkCompleted(string jobId, string resultVideoId) =>
        _jobs.AddOrUpdate(
            jobId,
            _ => new CompositionJobStatus(jobId, CompositionJobState.Completed, resultVideoId),
            (_, existing) => existing with { State = CompositionJobState.Completed, ResultVideoId = resultVideoId });

    public void MarkFailed(string jobId, string? diagnostic) =>
        _jobs.AddOrUpdate(
            jobId,
            _ => new CompositionJobStatus(jobId, CompositionJobState.Failed, Diagnostic: diagnostic),
            (_, existing) => existing with { State = CompositionJobState.Failed, Diagnostic = diagnostic });

    public IReadOnlyList<CompositionJobStatus> GetAll() =>
        _order
            .Select(jobId => _jobs.TryGetValue(jobId, out var status) ? status : null)
            .Where(status => status is not null)
            .Select(status => status!)
            .ToList();
}
