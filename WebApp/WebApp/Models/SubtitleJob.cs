namespace WebApp.Models;

internal sealed record SubtitleJob(
    string CacheKey,
    VideoFileEntry SourceEntry,
    SubtitleFileInfo Subtitle);
