namespace WebApp.Models;

internal sealed record SubtitleFileInfo(
    string PhysicalPath,
    long SizeBytes,
    DateTime LastWriteTimeUtc);
