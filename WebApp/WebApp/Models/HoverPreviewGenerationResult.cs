namespace WebApp.Models;

internal enum HoverPreviewGenerationStatus
{
    Success,
    Failed,
    Cancelled
}

internal sealed record HoverPreviewGenerationResult(HoverPreviewGenerationStatus Status, string? Diagnostic = null)
{
    public static HoverPreviewGenerationResult Success() => new(HoverPreviewGenerationStatus.Success);

    public static HoverPreviewGenerationResult Failed(string? diagnostic = null) =>
        new(HoverPreviewGenerationStatus.Failed, diagnostic);

    public static HoverPreviewGenerationResult Cancelled() => new(HoverPreviewGenerationStatus.Cancelled);
}
