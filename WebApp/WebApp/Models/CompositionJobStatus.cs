using WebApp.Client.Models;

namespace WebApp.Models;

internal sealed record CompositionJobStatus(
    string JobId,
    CompositionJobState State,
    string? ResultVideoId = null,
    string? Diagnostic = null);
