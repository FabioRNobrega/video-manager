namespace WebApp.Client.Models;

public sealed record ArchiveItemDto(
    string Id,
    string Name,
    ArchiveItemKind Kind,
    string? Extension,
    long? SizeBytes,
    DateTime LastWriteTimeUtc,
    bool IsVideo);
