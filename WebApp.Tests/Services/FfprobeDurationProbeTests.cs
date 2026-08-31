using System.Diagnostics;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class FfprobeDurationProbeTests
{
    [Fact]
    public async Task Returns_the_real_duration_of_a_known_fixture_when_ffmpeg_is_available()
    {
        if (!IsFfmpegAvailable())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var path = await CreateFixtureAsync(directory.Path, durationSeconds: 4);

        var duration = await new FfprobeDurationProbe().GetDurationAsync(path, CancellationToken.None);

        Assert.NotNull(duration);
        Assert.True(Math.Abs(duration.Value.TotalSeconds - 4) < 0.5,
            $"expected ~4s, got {duration.Value.TotalSeconds}s");
    }

    [Fact]
    public async Task Returns_null_for_a_missing_file()
    {
        using var directory = new TemporaryDirectory();

        var duration = await new FfprobeDurationProbe().GetDurationAsync(
            Path.Combine(directory.Path, "missing.mp4"), CancellationToken.None);

        Assert.Null(duration);
    }

    [Fact]
    public async Task Returns_null_for_a_corrupt_or_unsupported_file()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "corrupt.mp4");
        await File.WriteAllBytesAsync(path, [0x00, 0x01, 0x02, 0x03]);

        var duration = await new FfprobeDurationProbe().GetDurationAsync(path, CancellationToken.None);

        Assert.Null(duration);
    }

    [Fact]
    public async Task Honors_precancelled_tokens()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "clip.mp4");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new FfprobeDurationProbe().GetDurationAsync(path, cts.Token));
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

    private static async Task<string> CreateFixtureAsync(string directory, int durationSeconds)
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
            "-f", "lavfi", "-i", $"color=c=blue:s=320x240:d={durationSeconds}",
            path
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        await process.WaitForExitAsync();
        return path;
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
