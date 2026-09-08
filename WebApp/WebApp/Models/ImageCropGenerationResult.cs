namespace WebApp.Models;

internal enum ImageCropGenerationStatus
{
    Success,
    OutOfBounds,
    Failed
}

internal sealed record ImageCropGenerationResult(ImageCropGenerationStatus Status, string? Diagnostic = null)
{
    public static ImageCropGenerationResult Success() => new(ImageCropGenerationStatus.Success);

    public static ImageCropGenerationResult OutOfBounds(string? diagnostic = null) =>
        new(ImageCropGenerationStatus.OutOfBounds, diagnostic);

    public static ImageCropGenerationResult Failed(string? diagnostic = null) =>
        new(ImageCropGenerationStatus.Failed, diagnostic);
}
