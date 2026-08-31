using System.Diagnostics;
using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class FfmpegHoverPreviewGeneratorTests
{
    [Theory]
    [InlineData(0.1, 0, 0)]
    [InlineData(1, 0, 0.8)]
    [InlineData(2.99, 0, 2.79)]
    public void ComputeSamplePositions_below_three_seconds_returns_a_single_segment_from_zero(
        double durationSeconds, double expectedStart, double expectedLength)
    {
        var segments = FfmpegHoverPreviewGenerator.ComputeSamplePositions(
            TimeSpan.FromSeconds(durationSeconds), TimeSpan.FromSeconds(1.5));

        var segment = Assert.Single(segments);
        Assert.Equal(expectedStart, segment.Start.TotalSeconds, precision: 2);
        Assert.Equal(expectedLength, segment.Length.TotalSeconds, precision: 2);
    }

    [Theory]
    [InlineData(3, 0, 2.8)]
    [InlineData(10, 0, 3)]
    [InlineData(14.99, 0, 3)]
    public void ComputeSamplePositions_between_three_and_fifteen_seconds_returns_a_single_capped_segment(
        double durationSeconds, double expectedStart, double expectedLength)
    {
        var segments = FfmpegHoverPreviewGenerator.ComputeSamplePositions(
            TimeSpan.FromSeconds(durationSeconds), TimeSpan.FromSeconds(1.5));

        var segment = Assert.Single(segments);
        Assert.Equal(expectedStart, segment.Start.TotalSeconds, precision: 2);
        Assert.Equal(expectedLength, segment.Length.TotalSeconds, precision: 2);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(15.5)]
    [InlineData(20)]
    [InlineData(100)]
    [InlineData(3600)]
    public void ComputeSamplePositions_at_or_above_fifteen_seconds_returns_three_segments_at_20_50_80_percent(
        double durationSeconds)
    {
        var duration = TimeSpan.FromSeconds(durationSeconds);
        var segmentLength = TimeSpan.FromSeconds(1.5);

        var segments = FfmpegHoverPreviewGenerator.ComputeSamplePositions(duration, segmentLength);

        Assert.Equal(3, segments.Count);
        var limit = duration - TimeSpan.FromSeconds(0.2);
        foreach (var segment in segments)
        {
            Assert.True(segment.Start >= TimeSpan.Zero);
            Assert.True(segment.Start + segment.Length <= limit + TimeSpan.FromMilliseconds(1));
        }

        Assert.Equal(durationSeconds * 0.2, segments[0].Start.TotalSeconds, precision: 1);
        Assert.Equal(durationSeconds * 0.5, segments[1].Start.TotalSeconds, precision: 1);
        Assert.Equal(durationSeconds * 0.8, segments[2].Start.TotalSeconds, precision: 1);
    }

    [Fact]
    public void ComputeSamplePositions_clamps_the_last_segment_start_so_it_never_extends_past_the_source_end()
    {
        var duration = TimeSpan.FromSeconds(15);
        var segmentLength = TimeSpan.FromSeconds(3);

        var segments = FfmpegHoverPreviewGenerator.ComputeSamplePositions(duration, segmentLength);

        Assert.Equal(3, segments.Count);
        var limit = duration - TimeSpan.FromSeconds(0.2);
        foreach (var segment in segments)
        {
            Assert.True(segment.Start + segment.Length <= limit + TimeSpan.FromMilliseconds(1));
        }

        // The naive 80% start (12s) plus a 3s segment would reach 15s, past the 14.8s limit,
        // so the start must be pulled back rather than the segment silently overrunning the source.
        Assert.True(segments[2].Start < TimeSpan.FromSeconds(12));
    }

    [Fact]
    public void ComputeSamplePositions_returns_no_segments_for_a_non_positive_duration()
    {
        Assert.Empty(FfmpegHoverPreviewGenerator.ComputeSamplePositions(TimeSpan.Zero, TimeSpan.FromSeconds(1.5)));
        Assert.Empty(FfmpegHoverPreviewGenerator.ComputeSamplePositions(TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(1.5)));
    }

    [Fact]
    public void Arguments_for_a_three_segment_plan_contain_three_inputs_and_a_concat_filter()
    {
        const string source = "/videos/my clip; rm -rf ~ && echo $(whoami).mp4";
        var segments = new[]
        {
            new FfmpegHoverPreviewGenerator.HoverPreviewSegment(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1.5)),
            new FfmpegHoverPreviewGenerator.HoverPreviewSegment(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1.5)),
            new FfmpegHoverPreviewGenerator.HoverPreviewSegment(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(1.5)),
        };

        var arguments = FfmpegHoverPreviewGenerator.BuildArguments(source, "/previews/hover/key.tmp.mp4", segments, 480, 15);

        Assert.Equal(3, arguments.Count(argument => argument == "-i"));
        Assert.Equal(3, arguments.Count(argument => argument == source));
        Assert.Contains("-filter_complex", arguments);
        Assert.Contains(arguments, argument => argument.Contains("concat=n=3:v=1:a=0"));
        Assert.Contains("-an", arguments);
        Assert.DoesNotContain("-vf", arguments);
        Assert.Contains("-map", arguments);
        Assert.Contains("[outv]", arguments);
    }

    [Fact]
    public void Arguments_for_a_single_segment_plan_contain_exactly_one_input_and_a_scale_filter_without_concat()
    {
        const string source = "/videos/clip.mp4";
        var segments = new[]
        {
            new FfmpegHoverPreviewGenerator.HoverPreviewSegment(TimeSpan.Zero, TimeSpan.FromSeconds(3)),
        };

        var arguments = FfmpegHoverPreviewGenerator.BuildArguments(source, "/previews/hover/key.tmp.mp4", segments, 480, 15);

        Assert.Single(arguments, argument => argument == "-i");
        Assert.Contains("-vf", arguments);
        Assert.DoesNotContain("-filter_complex", arguments);
        Assert.DoesNotContain(arguments, argument => argument.Contains("concat"));
        Assert.Contains("-an", arguments);
    }

    [Fact]
    public void Arguments_keep_the_source_path_as_its_own_list_entry_even_with_shell_metacharacters()
    {
        const string sourceWithSpacesAndMetacharacters = "/videos/my clip; rm -rf ~ && echo $(whoami).mp4";
        var segments = new[] { new FfmpegHoverPreviewGenerator.HoverPreviewSegment(TimeSpan.Zero, TimeSpan.FromSeconds(3)) };

        var arguments = FfmpegHoverPreviewGenerator.BuildArguments(
            sourceWithSpacesAndMetacharacters, "/previews/hover/key.tmp.mp4", segments, 480, 15);

        Assert.Contains(sourceWithSpacesAndMetacharacters, arguments);
        Assert.Equal("-i", arguments[arguments.ToList().IndexOf(sourceWithSpacesAndMetacharacters) - 1]);
    }

    [Fact]
    public void Temporary_path_is_unique_and_derived_from_the_destination()
    {
        const string destination = "/previews/hover/abc123.mp4";

        var first = FfmpegHoverPreviewGenerator.BuildTemporaryPath(destination);
        var second = FfmpegHoverPreviewGenerator.BuildTemporaryPath(destination);

        Assert.NotEqual(first, second);
        Assert.StartsWith("/previews/hover/abc123.", first);
        Assert.EndsWith(".tmp.mp4", first);
    }

    [Fact]
    public async Task Generate_rejects_a_destination_outside_the_preview_root()
    {
        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var sourcePath = await CreateSourceFileAsync(videos.Path);
        var generator = CreateGenerator(videos.Path, preview.Path);
        var entry = CreateEntry(sourcePath);

        var result = await generator.GenerateAsync(entry, "/etc/outside.mp4", CancellationToken.None);

        Assert.Equal(HoverPreviewGenerationStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Generate_rejects_a_source_that_changed_before_generation()
    {
        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var sourcePath = await CreateSourceFileAsync(videos.Path);
        var generator = CreateGenerator(videos.Path, preview.Path);
        var staleEntry = CreateEntry(sourcePath) with { SizeBytes = 999_999 };
        var destination = Path.Combine(preview.Path, "hover", "key.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var result = await generator.GenerateAsync(staleEntry, destination, CancellationToken.None);

        Assert.Equal(HoverPreviewGenerationStatus.Failed, result.Status);
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task Generate_treats_a_missing_source_as_changed()
    {
        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var generator = CreateGenerator(videos.Path, preview.Path);
        var entry = CreateEntry(Path.Combine(videos.Path, "missing.mp4"));
        var destination = Path.Combine(preview.Path, "hover", "key.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var result = await generator.GenerateAsync(entry, destination, CancellationToken.None);

        Assert.Equal(HoverPreviewGenerationStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Generate_returns_success_without_invoking_ffmpeg_when_a_valid_final_file_already_exists()
    {
        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var sourcePath = await CreateSourceFileAsync(videos.Path);
        var generator = CreateGenerator(videos.Path, preview.Path);
        var entry = CreateEntry(sourcePath);
        var destination = Path.Combine(preview.Path, "hover", "already-ready.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllBytesAsync(destination, [1, 2, 3]);
        var originalBytes = await File.ReadAllBytesAsync(destination);

        var result = await generator.GenerateAsync(entry, destination, CancellationToken.None);

        Assert.Equal(HoverPreviewGenerationStatus.Success, result.Status);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task Generate_fails_cleanly_without_starting_ffmpeg_when_duration_cannot_be_determined()
    {
        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var sourcePath = await CreateSourceFileAsync(videos.Path);
        var generator = CreateGenerator(videos.Path, preview.Path, new FixedDurationProbe(null));
        var entry = CreateEntry(sourcePath);
        var destination = Path.Combine(preview.Path, "hover", "key.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var result = await generator.GenerateAsync(entry, destination, CancellationToken.None);

        Assert.Equal(HoverPreviewGenerationStatus.Failed, result.Status);
        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(destination)!, "*.tmp.mp4"));
    }

    [Theory]
    [MemberData(nameof(NonPositiveDurations))]
    public async Task Generate_fails_cleanly_for_zero_or_negative_probed_durations(TimeSpan? duration)
    {
        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var sourcePath = await CreateSourceFileAsync(videos.Path);
        var generator = CreateGenerator(videos.Path, preview.Path, new FixedDurationProbe(duration));
        var entry = CreateEntry(sourcePath);
        var destination = Path.Combine(preview.Path, "hover", "key.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var result = await generator.GenerateAsync(entry, destination, CancellationToken.None);

        Assert.Equal(HoverPreviewGenerationStatus.Failed, result.Status);
        Assert.False(File.Exists(destination));
    }

    public static IEnumerable<object?[]> NonPositiveDurations()
    {
        yield return [TimeSpan.Zero];
        yield return [TimeSpan.FromSeconds(-5)];
    }

    [Fact]
    public async Task Generate_produces_a_verified_multi_segment_mp4_and_cleans_up_on_cancellation_when_ffmpeg_is_available()
    {
        if (!IsFfmpegAvailable())
        {
            return;
        }

        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var sourcePath = await CreateRealVideoFixtureAsync(videos.Path, durationSeconds: 20);
        var generator = CreateGenerator(videos.Path, preview.Path, new FixedDurationProbe(TimeSpan.FromSeconds(20)));
        var entry = CreateEntry(sourcePath);
        var destination = Path.Combine(preview.Path, "hover", "generated.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var result = await generator.GenerateAsync(entry, destination, CancellationToken.None);

        Assert.Equal(HoverPreviewGenerationStatus.Success, result.Status);
        Assert.True(File.Exists(destination));
        Assert.True(new FileInfo(destination).Length > 0);
        var probe = await ProbeAsync(destination);
        Assert.Equal(480, probe.Width);
        Assert.False(probe.HasAudio);
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(destination)!, "*.tmp.mp4"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelledDestination = Path.Combine(preview.Path, "hover", "cancelled.mp4");
        var cancelledResult = await generator.GenerateAsync(entry, cancelledDestination, cts.Token);

        Assert.Equal(HoverPreviewGenerationStatus.Cancelled, cancelledResult.Status);
        Assert.False(File.Exists(cancelledDestination));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(destination)!, "*.tmp.mp4"));
    }

    [Fact]
    public async Task Generate_produces_a_verified_single_segment_mp4_for_a_short_fixture_when_ffmpeg_is_available()
    {
        if (!IsFfmpegAvailable())
        {
            return;
        }

        using var preview = new TemporaryDirectory();
        using var videos = new TemporaryDirectory();
        var sourcePath = await CreateRealVideoFixtureAsync(videos.Path, durationSeconds: 2);
        var generator = CreateGenerator(videos.Path, preview.Path, new FixedDurationProbe(TimeSpan.FromSeconds(2)));
        var entry = CreateEntry(sourcePath);
        var destination = Path.Combine(preview.Path, "hover", "short.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var result = await generator.GenerateAsync(entry, destination, CancellationToken.None);

        Assert.Equal(HoverPreviewGenerationStatus.Success, result.Status);
        Assert.True(File.Exists(destination));
        Assert.True(new FileInfo(destination).Length > 0);
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
        var destination = Path.Combine(preview.Path, "hover", "corrupt.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var result = await generator.GenerateAsync(entry, destination, CancellationToken.None);

        Assert.Equal(HoverPreviewGenerationStatus.Failed, result.Status);
        Assert.False(File.Exists(destination));
        Assert.DoesNotContain(sourcePath, result.Diagnostic ?? string.Empty);
        Assert.DoesNotContain(videos.Path, result.Diagnostic ?? string.Empty);
    }

    private static async Task<(int Width, bool HasAudio)> ProbeAsync(string path)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[]
        {
            "-v", "error", "-show_entries", "stream=codec_type,width", "-of", "csv=p=0", path
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hasAudio = lines.Any(line => line.StartsWith("audio", StringComparison.OrdinalIgnoreCase));
        var videoLine = lines.First(line => line.StartsWith("video", StringComparison.OrdinalIgnoreCase));
        var width = int.Parse(videoLine.Split(',')[1]);
        return (width, hasAudio);
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

    private static async Task<string> CreateRealVideoFixtureAsync(string directory, int durationSeconds)
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
            "-f", "lavfi", "-i", $"testsrc=size=640x360:rate=15:duration={durationSeconds}", path
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

    private static FfmpegHoverPreviewGenerator CreateGenerator(
        string videoRoot, string previewRoot, IVideoDurationProbe? durationProbe = null) => new(
        Options.Create(new VideoLibraryOptions { Path = videoRoot }),
        Options.Create(new ThumbnailCacheOptions { Path = previewRoot }),
        Options.Create(new HoverPreviewOptions()),
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
