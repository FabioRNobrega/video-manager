using System.Diagnostics;
using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class FfmpegSubtitleGeneratorTests
{
    [Fact]
    public void Arguments_keep_subtitle_and_destination_as_separate_list_entries()
    {
        const string subtitle = "/videos/my subtitle; rm -rf ~ && echo $(whoami).srt";
        const string destination = "/previews/subtitles/key.tmp.vtt";

        var arguments = FfmpegSubtitleGenerator.BuildArguments(subtitle, destination);

        Assert.Contains(subtitle, arguments);
        Assert.Contains(destination, arguments);
        Assert.Equal("-i", arguments[Array.IndexOf(arguments.ToArray(), subtitle) - 1]);
        Assert.Equal("-y", arguments[Array.IndexOf(arguments.ToArray(), destination) - 1]);
    }

    [Fact]
    public void Temporary_path_is_unique_and_derived_from_destination()
    {
        const string destination = "/previews/subtitles/abc123.vtt";

        var first = FfmpegSubtitleGenerator.BuildTemporaryPath(destination);
        var second = FfmpegSubtitleGenerator.BuildTemporaryPath(destination);

        Assert.NotEqual(first, second);
        Assert.StartsWith("/previews/subtitles/abc123.", first);
        Assert.EndsWith(".tmp.vtt", first);
    }

    [Fact]
    public async Task Generate_returns_cancelled_when_token_is_already_cancelled()
    {
        using var preview = new TemporaryDirectory();
        using var media = new TemporaryDirectory();
        var (entry, subtitle) = await CreatePairAsync(media.Path);
        var generator = CreateGenerator(preview.Path);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await generator.GenerateAsync(entry, subtitle, Path.Combine(preview.Path, "subtitles", "cancelled.vtt"), cts.Token);

        Assert.Equal(SubtitleGenerationStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task Generate_rejects_changed_sources_without_a_final_file()
    {
        using var preview = new TemporaryDirectory();
        using var media = new TemporaryDirectory();
        var (entry, subtitle) = await CreatePairAsync(media.Path);
        var generator = CreateGenerator(preview.Path);

        var result = await generator.GenerateAsync(
            entry with { SizeBytes = entry.SizeBytes + 1 },
            subtitle,
            Path.Combine(preview.Path, "subtitles", "changed.vtt"),
            CancellationToken.None);

        Assert.Equal(SubtitleGenerationStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Generate_converts_valid_srt_and_fails_corrupt_srt_when_ffmpeg_is_available()
    {
        if (!IsFfmpegAvailable())
        {
            return;
        }

        using var preview = new TemporaryDirectory();
        using var media = new TemporaryDirectory();
        var (entry, subtitle) = await CreatePairAsync(media.Path);
        var generator = CreateGenerator(preview.Path);
        var destination = Path.Combine(preview.Path, "subtitles", "generated.vtt");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var result = await generator.GenerateAsync(entry, subtitle, destination, CancellationToken.None);

        Assert.Equal(SubtitleGenerationStatus.Success, result.Status);
        Assert.Contains("WEBVTT", await File.ReadAllTextAsync(destination));

        var corruptPath = Path.Combine(media.Path, "corrupt.srt");
        await File.WriteAllTextAsync(corruptPath, "not a subtitle");
        var corrupt = new SubtitleFileInfo(corruptPath, new FileInfo(corruptPath).Length, File.GetLastWriteTimeUtc(corruptPath));
        var failed = await generator.GenerateAsync(entry, corrupt, Path.Combine(preview.Path, "subtitles", "corrupt.vtt"), CancellationToken.None);

        Assert.Equal(SubtitleGenerationStatus.Failed, failed.Status);
    }

    private static FfmpegSubtitleGenerator CreateGenerator(string previewPath) =>
        new(Options.Create(new ThumbnailCacheOptions { Path = previewPath }));

    private static async Task<(VideoFileEntry Entry, SubtitleFileInfo Subtitle)> CreatePairAsync(string directory)
    {
        var videoPath = Path.Combine(directory, "clip.mp4");
        var subtitlePath = Path.Combine(directory, "clip.srt");
        await File.WriteAllBytesAsync(videoPath, [1, 2, 3]);
        await File.WriteAllTextAsync(subtitlePath, """
            1
            00:00:00,000 --> 00:00:01,000
            Hello

            """);
        var video = new FileInfo(videoPath);
        var subtitle = new FileInfo(subtitlePath);
        return (
            new VideoFileEntry("clip", videoPath, "clip.mp4", "clip.mp4", ".mp4", video.Length, video.LastWriteTimeUtc),
            new SubtitleFileInfo(subtitlePath, subtitle.Length, subtitle.LastWriteTimeUtc));
    }

    private static bool IsFfmpegAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                ArgumentList = { "-version" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            process?.WaitForExit(5000);
            return process is { ExitCode: 0 };
        }
        catch
        {
            return false;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-subtitle-generator-{Guid.NewGuid():N}");
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
