namespace WebApp.Client.Models;

public sealed record PlaylistTrackDto(
    string Id,
    string Name,
    string Extension,
    long SizeBytes,
    PersistentMediaKind MediaKind,
    string SourceUrl,
    string StreamBasePath,
    string? ThumbnailUrl,
    string? AlbumCoverUrl,
    double? DurationSeconds);
