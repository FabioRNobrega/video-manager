namespace WebApp.Client.Models;

public sealed record MusicTrackDto(
    string Id,
    string Name,
    string Extension,
    long SizeBytes,
    string AudioUrl,
    string? AlbumCoverUrl,
    double? DurationSeconds,
    string StreamBasePath);
