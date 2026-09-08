namespace WebApp.Client.Models;

public sealed record ArchiveItemDto(
    string Id,
    string Name,
    ArchiveItemKind Kind,
    string? Extension,
    long? SizeBytes,
    DateTime LastWriteTimeUtc,
    bool IsVideo,
    ThumbnailState ThumbnailState = ThumbnailState.Unavailable,
    string? ThumbnailUrl = null,
    HoverPreviewState HoverPreviewState = HoverPreviewState.Unavailable,
    string? HoverPreviewUrl = null,
    double? DurationSeconds = null,
    int? Width = null,
    int? Height = null);
