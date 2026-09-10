namespace WebApp.Models;

internal enum SubtitleGenerationStatus
{
    Success,
    Failed,
    Cancelled
}

internal sealed record SubtitleGenerationResult(SubtitleGenerationStatus Status, string? Diagnostic = null)
{
    public static SubtitleGenerationResult Success() => new(SubtitleGenerationStatus.Success);

    public static SubtitleGenerationResult Failed(string? diagnostic = null) =>
        new(SubtitleGenerationStatus.Failed, diagnostic);

    public static SubtitleGenerationResult Cancelled() => new(SubtitleGenerationStatus.Cancelled);
}
