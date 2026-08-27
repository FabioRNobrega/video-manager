namespace WebApp.Models;

internal sealed record VideoFileEntry(
    string Id,
    string PhysicalPath,
    string Name,
    string Extension,
    long SizeBytes);
