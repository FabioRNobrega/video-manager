namespace WebApp.Models;

internal enum CutGenerationStatus
{
    Success,
    Failed,
    Cancelled
}

internal sealed record CutGenerationResult(CutGenerationStatus Status, string? Diagnostic = null)
{
    public static CutGenerationResult Success() => new(CutGenerationStatus.Success);

    public static CutGenerationResult Failed(string? diagnostic = null) =>
        new(CutGenerationStatus.Failed, diagnostic);

    public static CutGenerationResult Cancelled() => new(CutGenerationStatus.Cancelled);
}
