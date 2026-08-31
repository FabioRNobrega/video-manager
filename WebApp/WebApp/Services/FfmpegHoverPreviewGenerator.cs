using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class FfmpegHoverPreviewGenerator(
    IOptions<VideoLibraryOptions> videoLibraryOptions,
    IOptions<ThumbnailCacheOptions> thumbnailCacheOptions,
    IOptions<HoverPreviewOptions> hoverPreviewOptions,
    IVideoDurationProbe durationProbe) : IHoverPreviewGenerator
{
    private const int MaxDiagnosticLength = 2000;

    private static readonly TimeSpan SafetyMargin = TimeSpan.FromSeconds(0.2);
    private static readonly TimeSpan ShortVideoThreshold = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan TinyVideoThreshold = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ShortVideoSegmentCap = TimeSpan.FromSeconds(3);
    private static readonly double[] SampleFractions = [0.2, 0.5, 0.8];

    private readonly string _videoRoot = Path.GetFullPath(videoLibraryOptions.Value.Path);
    private readonly string _previewRoot = Path.GetFullPath(Path.Combine(thumbnailCacheOptions.Value.Path, "hover"));

    public async Task<HoverPreviewGenerationResult> GenerateAsync(
        VideoFileEntry source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (!VideoLibraryService.IsWithinRoot(_previewRoot, destinationPath))
        {
            return HoverPreviewGenerationResult.Failed("destination outside the configured preview root");
        }

        if (!SourceMatches(source))
        {
            return HoverPreviewGenerationResult.Failed("source changed before generation started");
        }

        if (IsValidOutput(destinationPath))
        {
            return HoverPreviewGenerationResult.Success();
        }

        TimeSpan? duration;
        try
        {
            duration = await durationProbe.GetDurationAsync(source.PhysicalPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return HoverPreviewGenerationResult.Cancelled();
        }

        if (duration is not { } value || value <= TimeSpan.Zero)
        {
            return HoverPreviewGenerationResult.Failed("duration unavailable");
        }

        var segmentLength = TimeSpan.FromSeconds(Math.Max(0.1, hoverPreviewOptions.Value.SegmentSeconds));
        var segments = ComputeSamplePositions(value, segmentLength);
        if (segments.Count == 0)
        {
            return HoverPreviewGenerationResult.Failed("no sample segments computed");
        }

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

            foreach (var argument in BuildArguments(
                source.PhysicalPath,
                temporaryPath,
                segments,
                hoverPreviewOptions.Value.Width,
                hoverPreviewOptions.Value.FrameRate))
            {
                startInfo.ArgumentList.Add(argument);
            }

            process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return HoverPreviewGenerationResult.Failed("ffmpeg failed to start");
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
                return HoverPreviewGenerationResult.Cancelled();
            }

            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                TryDelete(temporaryPath);
                return HoverPreviewGenerationResult.Failed(
                    Redact($"ffmpeg exited with code {process.ExitCode}: {stderr}", source.PhysicalPath, temporaryPath, destinationPath));
            }

            if (!IsValidOutput(temporaryPath))
            {
                TryDelete(temporaryPath);
                return HoverPreviewGenerationResult.Failed("ffmpeg produced no readable output");
            }

            Publish(temporaryPath, destinationPath);
            return HoverPreviewGenerationResult.Success();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            TryDelete(temporaryPath);
            return HoverPreviewGenerationResult.Failed(
                Redact(exception.Message, source.PhysicalPath, temporaryPath, destinationPath));
        }
        finally
        {
            process?.Dispose();
        }
    }

    internal readonly record struct HoverPreviewSegment(TimeSpan Start, TimeSpan Length);

    internal static IReadOnlyList<HoverPreviewSegment> ComputeSamplePositions(TimeSpan duration, TimeSpan segmentLength)
    {
        if (duration <= TimeSpan.Zero)
        {
            return [];
        }

        if (duration < TinyVideoThreshold)
        {
            var length = duration - SafetyMargin;
            if (length < TimeSpan.Zero)
            {
                length = TimeSpan.Zero;
            }

            return [new HoverPreviewSegment(TimeSpan.Zero, length)];
        }

        if (duration < ShortVideoThreshold)
        {
            var upperBound = duration - SafetyMargin;
            if (upperBound < TimeSpan.Zero)
            {
                upperBound = TimeSpan.Zero;
            }

            var length = ShortVideoSegmentCap < upperBound ? ShortVideoSegmentCap : upperBound;
            return [new HoverPreviewSegment(TimeSpan.Zero, length)];
        }

        var limit = duration - SafetyMargin;
        var segments = new List<HoverPreviewSegment>(SampleFractions.Length);

        foreach (var fraction in SampleFractions)
        {
            var start = TimeSpan.FromTicks((long)(duration.Ticks * fraction));
            var maxStart = limit - segmentLength;
            if (maxStart < TimeSpan.Zero)
            {
                maxStart = TimeSpan.Zero;
            }

            if (start > maxStart)
            {
                start = maxStart;
            }

            var length = segmentLength;
            if (start + length > limit)
            {
                length = limit - start;
                if (length < TimeSpan.Zero)
                {
                    length = TimeSpan.Zero;
                }
            }

            segments.Add(new HoverPreviewSegment(start, length));
        }

        return segments;
    }

    internal static IReadOnlyList<string> BuildArguments(
        string sourcePath,
        string temporaryPath,
        IReadOnlyList<HoverPreviewSegment> segments,
        int width,
        int frameRate)
    {
        var arguments = new List<string> { "-nostdin", "-hide_banner", "-loglevel", "error" };

        foreach (var segment in segments)
        {
            arguments.Add("-ss");
            arguments.Add(segment.Start.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            arguments.Add("-t");
            arguments.Add(segment.Length.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            arguments.Add("-i");
            arguments.Add(sourcePath);
        }

        if (segments.Count == 1)
        {
            arguments.Add("-vf");
            arguments.Add(
                string.Create(CultureInfo.InvariantCulture, $"scale={width}:-2,fps={frameRate},setsar=1"));
        }
        else
        {
            var filterGraph = new StringBuilder();
            for (var index = 0; index < segments.Count; index++)
            {
                filterGraph.Append(
                    string.Create(CultureInfo.InvariantCulture, $"[{index}:v]scale={width}:-2,fps={frameRate},setsar=1[v{index}];"));
            }

            for (var index = 0; index < segments.Count; index++)
            {
                filterGraph.Append(string.Create(CultureInfo.InvariantCulture, $"[v{index}]"));
            }

            filterGraph.Append(string.Create(CultureInfo.InvariantCulture, $"concat=n={segments.Count}:v=1:a=0[outv]"));

            arguments.Add("-filter_complex");
            arguments.Add(filterGraph.ToString());
            arguments.Add("-map");
            arguments.Add("[outv]");
        }

        arguments.Add("-an");
        arguments.Add("-c:v");
        arguments.Add("libx264");
        arguments.Add("-preset");
        arguments.Add("veryfast");
        arguments.Add("-crf");
        arguments.Add("28");
        arguments.Add("-movflags");
        arguments.Add("+faststart");
        arguments.Add("-y");
        arguments.Add(temporaryPath);

        return arguments;
    }

    internal static string BuildTemporaryPath(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(destinationPath);
        return Path.Combine(directory, $"{stem}.{Guid.NewGuid():N}.tmp.mp4");
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
