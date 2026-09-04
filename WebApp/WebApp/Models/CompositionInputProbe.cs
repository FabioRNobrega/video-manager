namespace WebApp.Models;

internal sealed record CompositionInputProbe(
    int Width,
    int Height,
    TimeSpan Duration,
    double FrameRate,
    string VideoCodec,
    string? AudioCodec,
    int? AudioSampleRate,
    int? AudioChannels);
