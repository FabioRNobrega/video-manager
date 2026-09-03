using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class FfmpegCutGenerator(
    IOptions<VideoLibraryOptions> videoLibraryOptions,
    IOptions<VideoCutOptions> videoCutOptions,
    CutNamingService namingService) : ICutGenerator
{
    private const int MaxDiagnosticLength = 2000;

    private readonly string _videoRoot = Path.GetFullPath(videoLibraryOptions.Value.Path);
    private readonly string _cutRoot = Path.GetFullPath(videoCutOptions.Value.Path);

    public async Task<CutGenerationResult> GenerateAsync(CutJob job, CancellationToken cancellationToken)
    {
        if (!SourceMatches(job.SourceEntry))
        {
            return CutGenerationResult.Failed("source changed before generation started");
        }

        var destinationPath = namingService.GetNextPath(job.SourceEntry.Name);
        if (!VideoLibraryService.IsWithinRoot(_cutRoot, destinationPath))
        {
            return CutGenerationResult.Failed("destination outside the configured cuts root");
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

            foreach (var argument in BuildArguments(job.SourceEntry.PhysicalPath, temporaryPath, job.Start, job.End))
            {
                startInfo.ArgumentList.Add(argument);
            }

            process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return CutGenerationResult.Failed("ffmpeg failed to start");
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
                return CutGenerationResult.Cancelled();
            }

            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                TryDelete(temporaryPath);
                return CutGenerationResult.Failed(
                    Redact($"ffmpeg exited with code {process.ExitCode}: {stderr}", job.SourceEntry.PhysicalPath, temporaryPath, destinationPath));
            }

            if (!IsValidOutput(temporaryPath))
            {
                TryDelete(temporaryPath);
                return CutGenerationResult.Failed("ffmpeg produced no readable output");
            }

            File.Move(temporaryPath, destinationPath, overwrite: false);
            return CutGenerationResult.Success();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            TryDelete(temporaryPath);
            return CutGenerationResult.Failed(
                Redact(exception.Message, job.SourceEntry.PhysicalPath, temporaryPath, destinationPath));
        }
        finally
        {
            process?.Dispose();
            TryDelete(temporaryPath);
        }
    }

    internal static IReadOnlyList<string> BuildArguments(
        string sourcePath, string temporaryPath, TimeSpan start, TimeSpan end) =>
    [
        "-nostdin",
        "-hide_banner",
        "-loglevel", "error",
        "-ss", FormatTime(start),
        "-to", FormatTime(end),
        "-i", sourcePath,
        "-map", "0",
        "-c", "copy",
        "-avoid_negative_ts", "make_zero",
        "-y", temporaryPath
    ];

    internal static string BuildTemporaryPath(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(destinationPath);
        return Path.Combine(directory, $"{stem}.{Guid.NewGuid():N}.tmp.mp4");
    }

    private static string FormatTime(TimeSpan value) =>
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
            .Replace(_cutRoot, "<cuts-root>", StringComparison.Ordinal);

        return redacted.Length > MaxDiagnosticLength ? redacted[..MaxDiagnosticLength] : redacted;
    }
}
