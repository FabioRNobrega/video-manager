namespace WebApp.Models;

internal enum ImageCropOutcomeStatus
{
    Success,
    NotFound,
    OutOfBounds,
    WriteFailed
}

internal sealed record ImageCropOutcome(
    ImageCropOutcomeStatus Status, string? Id = null, string? Name = null, string? Diagnostic = null)
{
    public static ImageCropOutcome Success(string id, string name) =>
        new(ImageCropOutcomeStatus.Success, id, name);

    public static ImageCropOutcome NotFound() => new(ImageCropOutcomeStatus.NotFound);

    public static ImageCropOutcome OutOfBounds(string? diagnostic = null) =>
        new(ImageCropOutcomeStatus.OutOfBounds, Diagnostic: diagnostic);

    public static ImageCropOutcome WriteFailed(string? diagnostic = null) =>
        new(ImageCropOutcomeStatus.WriteFailed, Diagnostic: diagnostic);
}
