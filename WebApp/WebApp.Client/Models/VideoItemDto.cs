namespace WebApp.Client.Models;

public sealed record VideoItemDto(
    string Id,
    string Name,
    string Extension,
    long SizeBytes,
    ThumbnailState ThumbnailState,
    string? ThumbnailUrl);
