using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class FfmpegThumbnailGenerator(
    IOptions<VideoLibraryOptions> videoLibraryOptions,
    IOptions<ThumbnailCacheOptions> thumbnailCacheOptions,
    IVideoDurationProbe durationProbe) : IThumbnailGenerator
{
    private const int MaxDiagnosticLength = 2000;
    private const double SeekFraction = 0.1;

    internal static readonly TimeSpan MinSeek = TimeSpan.FromSeconds(2);
    internal static readonly TimeSpan MaxSeek = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan FallbackSeek = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SafetyMargin = TimeSpan.FromSeconds(0.5);

    private readonly string _videoRoot = Path.GetFullPath(videoLibraryOptions.Value.Path);
    private readonly string _previewRoot = Path.GetFullPath(thumbnailCacheOptions.Value.Path);

    public async Task<ThumbnailGenerationResult> GenerateAsync(
        VideoFileEntry source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (!VideoLibraryService.IsWithinRoot(_previewRoot, destinationPath))
        {
            return ThumbnailGenerationResult.Failed("destination outside the configured preview root");
        }

        if (!SourceMatches(source))
        {
            return ThumbnailGenerationResult.Failed("source changed before generation started");
        }

        if (IsValidOutput(destinationPath))
        {
            return ThumbnailGenerationResult.Success();
        }

        TimeSpan? duration;
        try
        {
            duration = await durationProbe.GetDurationAsync(source.PhysicalPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ThumbnailGenerationResult.Cancelled();
        }

        var seek = ComputeSeekTime(duration);
        var temporaryPath = BuildTemporaryPath(destinationPath);
        Process? process = null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = false,
                RedirectStandardInput = false,
                CreateNoWindow = true,
            };

            foreach (var argument in BuildArguments(source.PhysicalPath, temporaryPath, seek))
            {
                startInfo.ArgumentList.Add(argument);
            }

            process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return ThumbnailGenerationResult.Failed("ffmpeg failed to start");
            }

            var stderrTask = ReadBoundedAsync(process.StandardError, MaxDiagnosticLength);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None);
                await SafeAwaitAsync(stderrTask);
                TryDelete(temporaryPath);
                return ThumbnailGenerationResult.Cancelled();
            }

            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                TryDelete(temporaryPath);
                return ThumbnailGenerationResult.Failed(
                    Redact($"ffmpeg exited with code {process.ExitCode}: {stderr}", source.PhysicalPath, temporaryPath, destinationPath));
            }

            if (!IsValidOutput(temporaryPath))
            {
                TryDelete(temporaryPath);
                return ThumbnailGenerationResult.Failed("ffmpeg produced no readable output");
            }

            Publish(temporaryPath, destinationPath);
            return ThumbnailGenerationResult.Success();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            TryDelete(temporaryPath);
            return ThumbnailGenerationResult.Failed(
                Redact(exception.Message, source.PhysicalPath, temporaryPath, destinationPath));
        }
        finally
        {
            process?.Dispose();
        }
    }

    internal static IReadOnlyList<string> BuildArguments(string sourcePath, string temporaryPath, TimeSpan seek) =>
    [
        "-nostdin",
        "-hide_banner",
        "-loglevel", "error",
        "-ss", seek.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
        "-i", sourcePath,
        "-frames:v", "1",
        "-vf", "scale=640:360:force_original_aspect_ratio=increase,crop=640:360",
        "-q:v", "3",
        "-y", temporaryPath
    ];

    internal static TimeSpan ComputeSeekTime(TimeSpan? duration)
    {
        if (duration is not { } value || value <= TimeSpan.Zero)
        {
            return FallbackSeek;
        }

        var upperBound = value - SafetyMargin;
        if (upperBound <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var lowerBound = MinSeek < upperBound ? MinSeek : upperBound;
        var cap = MaxSeek < upperBound ? MaxSeek : upperBound;
        var target = TimeSpan.FromTicks((long)(value.Ticks * SeekFraction));

        if (target < lowerBound)
        {
            return lowerBound;
        }

        return target > cap ? cap : target;
    }

    internal static string BuildTemporaryPath(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(destinationPath);
        return Path.Combine(directory, $"{stem}.{Guid.NewGuid():N}.tmp.jpg");
    }

    private static bool SourceMatches(VideoFileEntry source)
    {
        try
        {
            var file = new FileInfo(source.PhysicalPath);
            return file.Exists && file.Length == source.SizeBytes && file.LastWriteTimeUtc == source.LastWriteTimeUtc;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsValidOutput(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists && file.Length > 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Publish(string temporaryPath, string destinationPath)
    {
        try
        {
            if (IsValidOutput(destinationPath))
            {
                TryDelete(temporaryPath);
                return;
            }

            File.Move(temporaryPath, destinationPath, overwrite: false);
        }
        catch (IOException)
        {
            // A concurrent writer may have published first; the temporary file is cleaned up below.
            TryDelete(temporaryPath);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int maxLength)
    {
        var buffer = new char[4096];
        var builder = new StringBuilder();

        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer)) > 0)
            {
                if (builder.Length < maxLength)
                {
                    builder.Append(buffer, 0, Math.Min(read, maxLength - builder.Length));
                }
            }
        }
        catch (IOException)
        {
        }

        return builder.ToString();
    }

    private static async Task SafeAwaitAsync(Task<string> task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
        }
    }

    private string Redact(string diagnostic, string sourcePath, string temporaryPath, string destinationPath)
    {
        var redacted = diagnostic
            .Replace(sourcePath, "<source>", StringComparison.Ordinal)
            .Replace(temporaryPath, "<temp>", StringComparison.Ordinal)
            .Replace(destinationPath, "<final>", StringComparison.Ordinal)
            .Replace(_videoRoot, "<video-root>", StringComparison.Ordinal)
            .Replace(_previewRoot, "<preview-root>", StringComparison.Ordinal);

        return redacted.Length > MaxDiagnosticLength ? redacted[..MaxDiagnosticLength] : redacted;
    }
}
