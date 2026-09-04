namespace WebApp.Models;

internal enum CompositionStage
{
    Probe,
    Normalize,
    Concat
}

internal enum CompositionGenerationStatus
{
    Success,
    Failed,
    Cancelled
}

internal sealed record CompositionGenerationResult(
    CompositionGenerationStatus Status,
    CompositionStage? Stage = null,
    string? Diagnostic = null,
    string? DestinationPath = null)
{
    public static CompositionGenerationResult Success(string destinationPath) =>
        new(CompositionGenerationStatus.Success, DestinationPath: destinationPath);

    public static CompositionGenerationResult Failed(CompositionStage stage, string? diagnostic = null) =>
        new(CompositionGenerationStatus.Failed, stage, diagnostic);

    public static CompositionGenerationResult Cancelled() => new(CompositionGenerationStatus.Cancelled);
}
