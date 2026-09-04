using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class FfmpegCompositionGenerator(
    IOptions<VideoCutOptions> videoCutOptions,
    IOptions<VideoCompositionOptions> videoCompositionOptions,
    CompositionNamingService namingService,
    IVideoCompositionProbe probe) : ICompositionGenerator
{
    private const int MaxDiagnosticLength = 2000;

    private readonly string _cutRoot = Path.GetFullPath(videoCutOptions.Value.Path);
    private readonly string _compositionRoot = Path.GetFullPath(videoCompositionOptions.Value.Path);
    private readonly TimeSpan _transitionDuration = TimeSpan.FromSeconds(
        Math.Max(0.1, videoCompositionOptions.Value.TransitionDurationSeconds));

    public async Task<CompositionGenerationResult> GenerateAsync(CompositionJob job, CancellationToken cancellationToken)
    {
        var pathPlaceholders = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < job.OrderedSources.Count; index++)
        {
            pathPlaceholders[job.OrderedSources[index].PhysicalPath] = $"<source-{index}>";
        }

        var probes = new List<CompositionInputProbe>(job.OrderedSources.Count);
        foreach (var source in job.OrderedSources)
        {
            if (!SourceMatches(source))
            {
                return CompositionGenerationResult.Failed(
                    CompositionStage.Probe, Redact($"source changed before generation started: {source.PhysicalPath}", pathPlaceholders));
            }

            CompositionInputProbe? sourceProbe;
            try
            {
                sourceProbe = await probe.ProbeAsync(source.PhysicalPath, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return CompositionGenerationResult.Cancelled();
            }

            if (sourceProbe is null)
            {
                return CompositionGenerationResult.Failed(
                    CompositionStage.Probe, Redact($"ffprobe failed for {source.PhysicalPath}", pathPlaceholders));
            }

            if (sourceProbe.AudioCodec is null)
            {
                return CompositionGenerationResult.Failed(
                    CompositionStage.Probe, Redact($"no audio stream detected in {source.PhysicalPath}", pathPlaceholders));
            }

            probes.Add(sourceProbe);
        }

        var plan = CompositionFormatPlanner.Plan(probes);
        var fadePlans = CompositionFadePlanner.Plan(probes.Select(p => p.Duration).ToList(), _transitionDuration);

        var destinationPath = namingService.GetNextPath(job.OrderedSources[0].Name);
        if (!VideoLibraryService.IsWithinRoot(_compositionRoot, destinationPath))
        {
            return CompositionGenerationResult.Failed(CompositionStage.Concat, "destination outside the configured composition root");
        }

        pathPlaceholders[destinationPath] = "<final>";
        var normalizedPaths = job.OrderedSources
            .Select((_, index) => BuildTemporaryNormalizedPath(destinationPath, index))
            .ToList();
        for (var index = 0; index < normalizedPaths.Count; index++)
        {
            pathPlaceholders[normalizedPaths[index]] = $"<temp-{index}>";
        }

        var temporaryFinalPath = BuildTemporaryPath(destinationPath);
        var fileListPath = BuildTemporaryFileListPath(destinationPath);
        pathPlaceholders[temporaryFinalPath] = "<temp-final>";
        pathPlaceholders[fileListPath] = "<filelist>";

        try
        {
            for (var index = 0; index < job.OrderedSources.Count; index++)
            {
                var source = job.OrderedSources[index];
                var normalizedPath = normalizedPaths[index];

                var failure = await RunFfmpegAsync(
                    BuildNormalizeArguments(source.PhysicalPath, normalizedPath, plan, fadePlans[index]), cancellationToken);

                if (failure is { Cancelled: true })
                {
                    return CompositionGenerationResult.Cancelled();
                }

                if (failure is not null)
                {
                    return CompositionGenerationResult.Failed(
                        CompositionStage.Normalize, Redact($"ffmpeg normalize failed: {failure.Value.Diagnostic}", pathPlaceholders));
                }

                if (!IsValidOutput(normalizedPath))
                {
                    return CompositionGenerationResult.Failed(CompositionStage.Normalize, "ffmpeg produced no readable normalized output");
                }
            }

            await File.WriteAllTextAsync(fileListPath, BuildConcatFileList(normalizedPaths), cancellationToken);

            var concatFailure = await RunFfmpegAsync(BuildConcatArguments(fileListPath, temporaryFinalPath), cancellationToken);
            if (concatFailure is { Cancelled: true })
            {
                return CompositionGenerationResult.Cancelled();
            }

            if (concatFailure is not null)
            {
                return CompositionGenerationResult.Failed(
                    CompositionStage.Concat, Redact($"ffmpeg concat failed: {concatFailure.Value.Diagnostic}", pathPlaceholders));
            }

            if (!IsValidOutput(temporaryFinalPath))
            {
                return CompositionGenerationResult.Failed(CompositionStage.Concat, "ffmpeg produced no readable final output");
            }

            File.Move(temporaryFinalPath, destinationPath, overwrite: false);
            return CompositionGenerationResult.Success(destinationPath);
        }
        finally
        {
            foreach (var normalizedPath in normalizedPaths)
            {
                TryDelete(normalizedPath);
            }

            TryDelete(fileListPath);
            TryDelete(temporaryFinalPath);
        }
    }

    internal static IReadOnlyList<string> BuildNormalizeArguments(
        string sourcePath, string temporaryPath, CompositionFormatPlan plan, CompositionFadePlan fade)
    {
        var videoFilters = new List<string>
        {
            $"scale={plan.Width}:{plan.Height}:force_original_aspect_ratio=decrease",
            $"pad={plan.Width}:{plan.Height}:(ow-iw)/2:(oh-ih)/2",
            "setsar=1",
            $"fps={plan.FrameRate.ToString("0.####", CultureInfo.InvariantCulture)}"
        };

        var audioFilters = new List<string>
        {
            $"aresample={plan.AudioSampleRate}",
            $"aformat=channel_layouts={(plan.AudioChannels == 1 ? "mono" : "stereo")}"
        };

        if (fade.FadeInStart is { } fadeInStart && fade.FadeInDuration is { } fadeInDuration)
        {
            videoFilters.Add($"fade=t=in:st={FormatSeconds(fadeInStart)}:d={FormatSeconds(fadeInDuration)}");
            audioFilters.Add($"afade=t=in:st={FormatSeconds(fadeInStart)}:d={FormatSeconds(fadeInDuration)}");
        }

        if (fade.FadeOutStart is { } fadeOutStart && fade.FadeOutDuration is { } fadeOutDuration)
        {
            videoFilters.Add($"fade=t=out:st={FormatSeconds(fadeOutStart)}:d={FormatSeconds(fadeOutDuration)}");
            audioFilters.Add($"afade=t=out:st={FormatSeconds(fadeOutStart)}:d={FormatSeconds(fadeOutDuration)}");
        }

        return
        [
            "-nostdin",
            "-hide_banner",
            "-loglevel", "error",
            "-i", sourcePath,
            "-vf", string.Join(',', videoFilters),
            "-af", string.Join(',', audioFilters),
            "-c:v", "libx264",
            "-c:a", "aac",
            "-y", temporaryPath
        ];
    }

    internal static IReadOnlyList<string> BuildConcatArguments(string fileListPath, string destinationPath) =>
    [
        "-nostdin",
        "-hide_banner",
        "-loglevel", "error",
        "-f", "concat",
        "-safe", "0",
        "-i", fileListPath,
        "-c", "copy",
        "-y", destinationPath
    ];

    internal static string BuildConcatFileList(IReadOnlyList<string> normalizedPaths) =>
        string.Join('\n', normalizedPaths.Select(path => $"file '{path.Replace("'", "'\\''", StringComparison.Ordinal)}'"));

    internal static string BuildTemporaryPath(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(destinationPath);
        return Path.Combine(directory, $"{stem}.{Guid.NewGuid():N}.tmp.mp4");
    }

    internal static string BuildTemporaryNormalizedPath(string destinationPath, int index)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(destinationPath);
        return Path.Combine(directory, $"{stem}.{Guid.NewGuid():N}.part{index:0000}.tmp.mp4");
    }

    internal static string BuildTemporaryFileListPath(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(destinationPath);
        return Path.Combine(directory, $"{stem}.{Guid.NewGuid():N}.filelist.txt");
    }

    private static string FormatSeconds(TimeSpan value) =>
        value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

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

    private async Task<FfmpegFailure?> RunFfmpegAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
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

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process? process = null;
        try
        {
            process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return new FfmpegFailure(false, "ffmpeg failed to start");
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
                return new FfmpegFailure(true, null);
            }

            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                return new FfmpegFailure(false, $"ffmpeg exited with code {process.ExitCode}: {stderr}");
            }

            return null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            return new FfmpegFailure(false, exception.Message);
        }
        finally
        {
            process?.Dispose();
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

    private string Redact(string diagnostic, IReadOnlyDictionary<string, string> pathPlaceholders)
    {
        var redacted = diagnostic;
        foreach (var (path, placeholder) in pathPlaceholders)
        {
            redacted = redacted.Replace(path, placeholder, StringComparison.Ordinal);
        }

        redacted = redacted
            .Replace(_cutRoot, "<cuts-root>", StringComparison.Ordinal)
            .Replace(_compositionRoot, "<composition-root>", StringComparison.Ordinal);

        return redacted.Length > MaxDiagnosticLength ? redacted[..MaxDiagnosticLength] : redacted;
    }

    private readonly record struct FfmpegFailure(bool Cancelled, string? Diagnostic);
}
