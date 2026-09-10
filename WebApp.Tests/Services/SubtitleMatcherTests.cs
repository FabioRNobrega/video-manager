using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class SubtitleMatcherTests
{
    [Fact]
    public async Task Finds_same_basename_srt_next_to_video()
    {
        using var directory = new TemporaryDirectory();
        var videoPath = Path.Combine(directory.Path, "movie.mp4");
        var subtitlePath = Path.Combine(directory.Path, "movie.srt");
        await File.WriteAllBytesAsync(videoPath, [1, 2, 3]);
        await File.WriteAllTextAsync(subtitlePath, "subtitle");

        var match = new SubtitleMatcher().FindMatch(CreateEntry(videoPath, "movie.mp4"));

        Assert.NotNull(match);
        Assert.Equal(Path.GetFullPath(subtitlePath), match.PhysicalPath);
        Assert.Equal(new FileInfo(subtitlePath).Length, match.SizeBytes);
    }

    [Fact]
    public async Task Ignores_missing_different_folder_and_different_case_subtitles()
    {
        using var directory = new TemporaryDirectory();
        var videoPath = Path.Combine(directory.Path, "movie.mp4");
        await File.WriteAllBytesAsync(videoPath, [1]);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "other.srt"), "subtitle");
        Directory.CreateDirectory(Path.Combine(directory.Path, "nested"));
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "nested", "movie.srt"), "subtitle");
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "movie.SRT"), "subtitle");

        var match = new SubtitleMatcher().FindMatch(CreateEntry(videoPath, "movie.mp4"));

        Assert.Null(match);
    }

    [Fact]
    public async Task Falls_back_to_subtitle_srt_in_the_same_folder()
    {
        using var directory = new TemporaryDirectory();
        var videoPath = Path.Combine(directory.Path, "movie.mp4");
        var subtitlePath = Path.Combine(directory.Path, "subtitle.srt");
        await File.WriteAllBytesAsync(videoPath, [1]);
        await File.WriteAllTextAsync(subtitlePath, "subtitle");

        var match = new SubtitleMatcher().FindMatch(CreateEntry(videoPath, "movie.mp4"));

        Assert.NotNull(match);
        Assert.Equal(Path.GetFullPath(subtitlePath), match.PhysicalPath);
    }

    [Fact]
    public async Task Prefers_same_basename_srt_over_subtitle_srt()
    {
        using var directory = new TemporaryDirectory();
        var videoPath = Path.Combine(directory.Path, "movie.mp4");
        var sameNamePath = Path.Combine(directory.Path, "movie.srt");
        await File.WriteAllBytesAsync(videoPath, [1]);
        await File.WriteAllTextAsync(sameNamePath, "same name");
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "subtitle.srt"), "generic");

        var match = new SubtitleMatcher().FindMatch(CreateEntry(videoPath, "movie.mp4"));

        Assert.NotNull(match);
        Assert.Equal(Path.GetFullPath(sameNamePath), match.PhysicalPath);
    }

    [Fact]
    public async Task Works_for_archive_shaped_media_entries()
    {
        using var directory = new TemporaryDirectory();
        var videoPath = Path.Combine(directory.Path, "clip.mp4");
        await File.WriteAllBytesAsync(videoPath, [1, 2, 3]);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "clip.srt"), "subtitle");

        var match = new SubtitleMatcher().FindMatch(CreateEntry(videoPath, "archive/downloads/id/clip.mp4"));

        Assert.NotNull(match);
    }

    private static VideoFileEntry CreateEntry(string physicalPath, string relativePath) => new(
        Guid.NewGuid().ToString("N"),
        physicalPath,
        relativePath,
        Path.GetFileName(physicalPath),
        Path.GetExtension(physicalPath),
        new FileInfo(physicalPath).Length,
        File.GetLastWriteTimeUtc(physicalPath));

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-subtitle-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
