namespace WebApp.Models;

internal sealed record ThumbnailJob(string CacheKey, VideoFileEntry SourceEntry);
