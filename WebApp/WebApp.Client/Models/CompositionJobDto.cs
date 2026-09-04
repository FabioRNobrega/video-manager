namespace WebApp.Client.Models;

public sealed record CompositionJobDto(
    string JobId,
    CompositionJobState State,
    string? ResultVideoId,
    string? Diagnostic);
