using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class FfprobeCompositionProbe : IVideoCompositionProbe
{
    public async Task<CompositionInputProbe?> ProbeAsync(string physicalPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true,
        };

        foreach (var argument in new[]
        {
            "-v", "error",
            "-show_entries", "stream=codec_type,codec_name,width,height,r_frame_rate,sample_rate,channels",
            "-show_entries", "format=duration",
            "-of", "json",
            physicalPath
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process? process = null;
        try
        {
            process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return null;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None);
                throw;
            }

            if (process.ExitCode != 0)
            {
                return null;
            }

            var output = await stdoutTask;
            return Parse(output);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or Win32Exception or JsonException)
        {
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    internal static CompositionInputProbe? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("format", out var format) ||
            !format.TryGetProperty("duration", out var durationProperty) ||
            !double.TryParse(durationProperty.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var durationSeconds) ||
            durationSeconds <= 0)
        {
            return null;
        }

        if (!root.TryGetProperty("streams", out var streams))
        {
            return null;
        }

        JsonElement? videoStream = null;
        JsonElement? audioStream = null;
        foreach (var stream in streams.EnumerateArray())
        {
            var codecType = stream.TryGetProperty("codec_type", out var codecTypeProperty) ? codecTypeProperty.GetString() : null;
            if (codecType == "video" && videoStream is null)
            {
                videoStream = stream;
            }
            else if (codecType == "audio" && audioStream is null)
            {
                audioStream = stream;
            }
        }

        if (videoStream is null)
        {
            return null;
        }

        var video = videoStream.Value;
        if (!video.TryGetProperty("width", out var widthProperty) || !widthProperty.TryGetInt32(out var width) || width <= 0 ||
            !video.TryGetProperty("height", out var heightProperty) || !heightProperty.TryGetInt32(out var height) || height <= 0)
        {
            return null;
        }

        var videoCodec = video.TryGetProperty("codec_name", out var videoCodecProperty)
            ? videoCodecProperty.GetString() ?? "unknown"
            : "unknown";

        var frameRate = ParseFrameRate(
            video.TryGetProperty("r_frame_rate", out var frameRateProperty) ? frameRateProperty.GetString() : null);
        if (frameRate is not > 0)
        {
            return null;
        }

        string? audioCodec = null;
        int? audioSampleRate = null;
        int? audioChannels = null;
        if (audioStream is not null)
        {
            var audio = audioStream.Value;
            audioCodec = audio.TryGetProperty("codec_name", out var audioCodecProperty) ? audioCodecProperty.GetString() : null;
            audioSampleRate = audio.TryGetProperty("sample_rate", out var sampleRateProperty) &&
                int.TryParse(sampleRateProperty.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sampleRate)
                    ? sampleRate
                    : null;
            audioChannels = audio.TryGetProperty("channels", out var channelsProperty) && channelsProperty.TryGetInt32(out var channels)
                ? channels
                : null;
        }

        return new CompositionInputProbe(
            width, height, TimeSpan.FromSeconds(durationSeconds), frameRate.Value, videoCodec, audioCodec, audioSampleRate, audioChannels);
    }

    private static double? ParseFrameRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) &&
            denominator > 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var direct) && direct > 0
            ? direct
            : null;
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
}
