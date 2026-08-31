namespace WebApp.Models;

internal sealed record VideoFileEntry(
    string Id,
    string PhysicalPath,
    string RelativePath,
    string Name,
    string Extension,
    long SizeBytes,
    DateTime LastWriteTimeUtc);
