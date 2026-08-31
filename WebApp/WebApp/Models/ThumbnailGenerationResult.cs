namespace WebApp.Models;

internal enum ThumbnailGenerationStatus
{
    Success,
    Failed,
    Cancelled
}

internal sealed record ThumbnailGenerationResult(ThumbnailGenerationStatus Status, string? Diagnostic = null)
{
    public static ThumbnailGenerationResult Success() => new(ThumbnailGenerationStatus.Success);

    public static ThumbnailGenerationResult Failed(string? diagnostic = null) =>
        new(ThumbnailGenerationStatus.Failed, diagnostic);

    public static ThumbnailGenerationResult Cancelled() => new(ThumbnailGenerationStatus.Cancelled);
}
