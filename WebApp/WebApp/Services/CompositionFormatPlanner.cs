using WebApp.Models;

namespace WebApp.Services;

/// <summary>
/// Pure canvas/frame-rate/audio-format selection logic (FR8/FR9): no I/O, easily unit-tested directly.
/// </summary>
internal static class CompositionFormatPlanner
{
    private const int TargetAudioSampleRate = 48000;
    private const int TargetAudioChannels = 2;

    public static CompositionFormatPlan Plan(IReadOnlyList<CompositionInputProbe> probes)
    {
        if (probes.Count == 0)
        {
            throw new ArgumentException("At least one probe is required.", nameof(probes));
        }

        var canvas = probes
            .Select((probe, index) => (probe, index))
            .OrderBy(entry => (long)entry.probe.Width * entry.probe.Height)
            .ThenBy(entry => entry.index)
            .First()
            .probe;

        var frameRate = probes.Min(probe => probe.FrameRate);

        return new CompositionFormatPlan(canvas.Width, canvas.Height, frameRate, TargetAudioSampleRate, TargetAudioChannels);
    }
}
