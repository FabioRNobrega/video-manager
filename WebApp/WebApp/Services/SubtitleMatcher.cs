using WebApp.Models;

namespace WebApp.Services;

internal sealed class SubtitleMatcher
{
    public SubtitleFileInfo? FindMatch(VideoFileEntry entry)
    {
        try
        {
            return TryCreate(Path.ChangeExtension(entry.PhysicalPath, ".srt")) ??
                TryCreate(Path.Combine(Path.GetDirectoryName(entry.PhysicalPath) ?? string.Empty, "subtitle.srt"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static SubtitleFileInfo? TryCreate(string? subtitlePath)
    {
        if (string.IsNullOrWhiteSpace(subtitlePath))
        {
            return null;
        }

        var file = new FileInfo(subtitlePath);
        return file.Exists
            ? new SubtitleFileInfo(file.FullName, file.Length, file.LastWriteTimeUtc)
            : null;
    }
}
