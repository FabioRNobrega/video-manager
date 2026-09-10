using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class FfmpegSubtitleGenerator(IOptions<ThumbnailCacheOptions> thumbnailCacheOptions) : ISubtitleGenerator
{
    private const int MaxDiagnosticLength = 2000;

    private readonly string _previewRoot = Path.GetFullPath(thumbnailCacheOptions.Value.Path);

    public async Task<SubtitleGenerationResult> GenerateAsync(
        VideoFileEntry source,
        SubtitleFileInfo subtitle,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return SubtitleGenerationResult.Cancelled();
        }

        if (!VideoLibraryService.IsWithinRoot(_previewRoot, destinationPath))
        {
            return SubtitleGenerationResult.Failed("destination outside the configured preview root");
        }

        if (!SourceMatches(source) || !SubtitleMatches(subtitle))
        {
            return SubtitleGenerationResult.Failed("source changed before generation started");
        }

        if (IsValidOutput(destinationPath))
        {
            return SubtitleGenerationResult.Success();
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

            foreach (var argument in BuildArguments(subtitle.PhysicalPath, temporaryPath))
            {
                startInfo.ArgumentList.Add(argument);
            }

            process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return SubtitleGenerationResult.Failed("ffmpeg failed to start");
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
                return SubtitleGenerationResult.Cancelled();
            }

            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                TryDelete(temporaryPath);
                return SubtitleGenerationResult.Failed(
                    Redact($"ffmpeg exited with code {process.ExitCode}: {stderr}", subtitle.PhysicalPath, temporaryPath, destinationPath));
            }

            if (!IsValidOutput(temporaryPath))
            {
                TryDelete(temporaryPath);
                return SubtitleGenerationResult.Failed("ffmpeg produced no readable output");
            }

            Publish(temporaryPath, destinationPath);
            return SubtitleGenerationResult.Success();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            TryDelete(temporaryPath);
            return SubtitleGenerationResult.Failed(
                Redact(exception.Message, subtitle.PhysicalPath, temporaryPath, destinationPath));
        }
        finally
        {
            process?.Dispose();
        }
    }

    internal static IReadOnlyList<string> BuildArguments(string subtitlePath, string temporaryPath) =>
    [
        "-nostdin",
        "-hide_banner",
        "-loglevel", "error",
        "-i", subtitlePath,
        "-y", temporaryPath
    ];

    internal static string BuildTemporaryPath(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(destinationPath);
        return Path.Combine(directory, $"{stem}.{Guid.NewGuid():N}.tmp.vtt");
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

    private static bool SubtitleMatches(SubtitleFileInfo subtitle)
    {
        try
        {
            var file = new FileInfo(subtitle.PhysicalPath);
            return file.Exists && file.Length == subtitle.SizeBytes && file.LastWriteTimeUtc == subtitle.LastWriteTimeUtc;
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

    private string Redact(string diagnostic, string subtitlePath, string temporaryPath, string destinationPath)
    {
        var redacted = diagnostic
            .Replace(subtitlePath, "<subtitle>", StringComparison.Ordinal)
            .Replace(temporaryPath, "<temp>", StringComparison.Ordinal)
            .Replace(destinationPath, "<final>", StringComparison.Ordinal)
            .Replace(_previewRoot, "<preview-root>", StringComparison.Ordinal);

        return redacted.Length > MaxDiagnosticLength ? redacted[..MaxDiagnosticLength] : redacted;
    }
}
