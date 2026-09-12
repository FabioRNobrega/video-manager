using System.Diagnostics;

namespace WebApp.Services;

internal sealed class FfmpegAvailabilityProbe : IFfmpegAvailabilityProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    public bool IsAvailable() => CanRun("ffmpeg") && CanRun("ffprobe");

    private static bool CanRun(string executable)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                ArgumentList = { "-version" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                return false;
            }

            return process.WaitForExit(ProbeTimeout) && process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException)
        {
            return false;
        }
    }
}
