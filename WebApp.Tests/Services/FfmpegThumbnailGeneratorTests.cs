using System.Diagnostics;
using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class FfmpegThumbnailGeneratorTests
{
    [Fact]
    public void Arguments_keep_source_and_destination_as_separate_list_entries()
    {
        const string sourceWithSpacesAndMetacharacters = "/videos/my clip; rm -rf ~ && echo $(whoami).mp4";
        const string destination = "/previews/abc123.tmp.jpg";

        var arguments = FfmpegThumbnailGenerator.BuildArguments(
            sourceWithSpacesAndMetacharacters, destination, TimeSpan.FromSeconds(42));

        Assert.Contains(sourceWithSpacesAndMetacharacters, arguments);
        Assert.Contains(destination, arguments);
        Assert.DoesNotContain(arguments, argument => argument.Contains(' ') && argument != sourceWithSpacesAndMetacharacters);
        Assert.Equal("-i", arguments[Array.IndexOf(arguments.ToArray(), sourceWithSpacesAndMetacharacters) - 1]);
    }

    [Fact]
    public void Arguments_place_the_computed_seek_as_its_own_list_entry()
    {
        var arguments = FfmpegThumbnailGenerator.BuildArguments("/videos/clip.mp4", "/previews/key.tmp.jpg", TimeSpan.FromSeconds(125.5));

        var seekIndex = Array.IndexOf(arguments.ToArray(), "-ss");
        Assert.True(seekIndex >= 0);
        Assert.Equal("125.5", arguments[seekIndex + 1]);
    }

    [Theory]
    [InlineData(0.2, 0)]
    [InlineData(1, 0.5)]
    [InlineData(3, 2)]
    [InlineData(5, 2)]
    [InlineData(20, 2)]
    [InlineData(300, 30)]
    [InlineData(1200, 120)]
    [InlineData(10800, 600)]
    public void ComputeSeekTime_clamps_to_ten_percent_of_duration_between_the_floor_and_cap(
        double durationSeconds, double expectedSeekSeconds)
    {
        var seek = FfmpegThumbnailGenerator.ComputeSeekTime(TimeSpan.FromSeconds(durationSeconds));

        Assert.True(Math.Abs(seek.TotalSeconds - expectedSeekSeconds) < 0.05,
            $"expected ~{expectedSeekSeconds}s, got {seek.TotalSeconds}s");
    }

    [Fact]
    public void ComputeSeekTime_falls_back_to_a_fixed_offset_when_duration_is_unknown()
    {
        Assert.Equal(FfmpegThumbnailGenerator.FallbackSeek, FfmpegThumbnailGenerator.ComputeSeekTime(null));
        Assert.Equal(FfmpegThumbnailGenerator.FallbackSeek, FfmpegThumbnailGenerator.ComputeSeekTime(TimeSpan.Zero));
        Assert.Equal(FfmpegThumbnailGenerator.FallbackSeek, FfmpegThumbnailGenerator.ComputeSeekTime(TimeSpan.FromSeconds(-5)));
    }

    [Fact]
    public void ComputeSeekTime_never_exceeds_the_ten_minute_cap_for_very_long_videos()
    {
        var seek = FfmpegThumbnailGenerator.ComputeSeekTime(TimeSpan.FromHours(3));

        Assert.Equal(FfmpegThumbnailGenerator.MaxSeek, seek);
    }

    [Fact]
    public void Temporary_path_is_unique_and_derived_from_the_destination()
    {
        const string destination = "/previews/abc123.jpg";

        var first = FfmpegThumbnailGenerator.BuildTemporaryPath(destination);
        var second = FfmpegThumbnailGenerator.BuildTemporaryPath(destination);

        Assert.NotEqual(first, second);
        Assert.StartsWith("/previews/abc123.", first);
        Assert.EndsWith(".tmp.jpg", first);
    }

    [Fact]
    public async Task Generate_rejects_a_destination_outside_the_preview_root()
    {
        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var sourcePath = await CreateSourceFileAsync(videos.Path);
        var generator = CreateGenerator(videos.Path, preview.Path);
        var entry = CreateEntry(sourcePath);

        var result = await generator.GenerateAsync(entry, "/etc/outside.jpg", CancellationToken.None);

        Assert.Equal(ThumbnailGenerationStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Generate_rejects_a_source_that_changed_before_generation()
    {
        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var sourcePath = await CreateSourceFileAsync(videos.Path);
        var generator = CreateGenerator(videos.Path, preview.Path);
        var staleEntry = CreateEntry(sourcePath) with { SizeBytes = 999_999 };

        var result = await generator.GenerateAsync(
            staleEntry, Path.Combine(preview.Path, "key.jpg"), CancellationToken.None);

        Assert.Equal(ThumbnailGenerationStatus.Failed, result.Status);
        Assert.False(File.Exists(Path.Combine(preview.Path, "key.jpg")));
    }

    [Fact]
    public async Task Generate_treats_a_missing_source_as_changed()
    {
        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var generator = CreateGenerator(videos.Path, preview.Path);
        var entry = CreateEntry(Path.Combine(videos.Path, "missing.mp4"));

        var result = await generator.GenerateAsync(
            entry, Path.Combine(preview.Path, "key.jpg"), CancellationToken.None);

        Assert.Equal(ThumbnailGenerationStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Generate_returns_success_without_invoking_ffmpeg_when_a_valid_final_file_already_exists()
    {
        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var sourcePath = await CreateSourceFileAsync(videos.Path);
        var generator = CreateGenerator(videos.Path, preview.Path);
        var entry = CreateEntry(sourcePath);
        var destination = Path.Combine(preview.Path, "already-ready.jpg");
        await File.WriteAllBytesAsync(destination, [1, 2, 3]);
        var originalBytes = await File.ReadAllBytesAsync(destination);

        var result = await generator.GenerateAsync(entry, destination, CancellationToken.None);

        Assert.Equal(ThumbnailGenerationStatus.Success, result.Status);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task Generate_produces_a_verified_jpeg_and_cleans_up_on_cancellation_when_ffmpeg_is_available()
    {
        if (!IsFfmpegAvailable())
        {
            return;
        }

        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var sourcePath = await CreateRealVideoFixtureAsync(videos.Path);
        var generator = CreateGenerator(videos.Path, preview.Path, new FixedDurationProbe(TimeSpan.FromSeconds(5)));
        var entry = CreateEntry(sourcePath);
        var destination = Path.Combine(preview.Path, "generated.jpg");

        var result = await generator.GenerateAsync(entry, destination, CancellationToken.None);

        Assert.Equal(ThumbnailGenerationStatus.Success, result.Status);
        Assert.True(File.Exists(destination));
        Assert.True(new FileInfo(destination).Length > 0);
        var (width, height) = ReadJpegDimensions(destination);
        Assert.Equal(640, width);
        Assert.Equal(360, height);
        Assert.Empty(Directory.EnumerateFiles(preview.Path, "*.tmp.jpg"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelledDestination = Path.Combine(preview.Path, "cancelled.jpg");
        var cancelledResult = await generator.GenerateAsync(entry, cancelledDestination, cts.Token);

        Assert.Equal(ThumbnailGenerationStatus.Cancelled, cancelledResult.Status);
        Assert.False(File.Exists(cancelledDestination));
        Assert.Empty(Directory.EnumerateFiles(preview.Path, "*.tmp.jpg"));
    }

    [Fact]
    public async Task Generate_reports_failure_for_an_unsupported_or_corrupt_source_without_a_final_file()
    {
        if (!IsFfmpegAvailable())
        {
            return;
        }

        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var sourcePath = Path.Combine(videos.Path, "corrupt.mp4");
        await File.WriteAllBytesAsync(sourcePath, [0x00, 0x01, 0x02, 0x03]);
        var generator = CreateGenerator(videos.Path, preview.Path);
        var entry = CreateEntry(sourcePath);
        var destination = Path.Combine(preview.Path, "corrupt.jpg");

        var result = await generator.GenerateAsync(entry, destination, CancellationToken.None);

        Assert.Equal(ThumbnailGenerationStatus.Failed, result.Status);
        Assert.False(File.Exists(destination));
        Assert.DoesNotContain(sourcePath, result.Diagnostic ?? string.Empty);
        Assert.DoesNotContain(videos.Path, result.Diagnostic ?? string.Empty);
    }

    private static (int Width, int Height) ReadJpegDimensions(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8)
        {
            throw new InvalidOperationException("Not a JPEG file.");
        }

        while (stream.Position < stream.Length)
        {
            byte marker;
            do
            {
                marker = reader.ReadByte();
            } while (marker != 0xFF);

            byte type;
            do
            {
                type = reader.ReadByte();
            } while (type == 0xFF);

            if (type is 0xD8 or 0xD9 or 0x01 || (type >= 0xD0 && type <= 0xD7))
            {
                continue;
            }

            var length = (reader.ReadByte() << 8) | reader.ReadByte();
            if (type is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                reader.ReadByte();
                var height = (reader.ReadByte() << 8) | reader.ReadByte();
                var width = (reader.ReadByte() << 8) | reader.ReadByte();
                return (width, height);
            }

            stream.Seek(length - 2, SeekOrigin.Current);
        }

        throw new InvalidOperationException("No SOF marker found.");
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

    private static async Task<string> CreateSourceFileAsync(string directory)
    {
        var path = Path.Combine(directory, "clip.mp4");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4, 5]);
        return path;
    }

    private static async Task<string> CreateRealVideoFixtureAsync(string directory)
    {
        var path = Path.Combine(directory, "fixture.mp4");
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in new[]
        {
            "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
            "-f", "lavfi", "-i", "color=c=blue:s=320x240:d=5",
            "-frames:v", "150", path
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        await process.WaitForExitAsync();
        return path;
    }

    private static VideoFileEntry CreateEntry(string physicalPath)
    {
        var file = new FileInfo(physicalPath);
        return new VideoFileEntry(
            Guid.NewGuid().ToString("N"),
            physicalPath,
            Path.GetFileName(physicalPath),
            Path.GetFileName(physicalPath),
            Path.GetExtension(physicalPath),
            file.Exists ? file.Length : 0,
            file.Exists ? file.LastWriteTimeUtc : DateTime.UtcNow);
    }

    private static FfmpegThumbnailGenerator CreateGenerator(
        string videoRoot, string previewRoot, IVideoDurationProbe? durationProbe = null) => new(
        Options.Create(new VideoLibraryOptions { Path = videoRoot }),
        Options.Create(new ThumbnailCacheOptions { Path = previewRoot }),
        durationProbe ?? new FfprobeDurationProbe());

    private sealed class FixedDurationProbe(TimeSpan? duration) : IVideoDurationProbe
    {
        public Task<TimeSpan?> GetDurationAsync(string physicalPath, CancellationToken cancellationToken) =>
            Task.FromResult(duration);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-tests-{Guid.NewGuid():N}");
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
