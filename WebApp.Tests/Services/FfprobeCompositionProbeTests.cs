using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class FfprobeCompositionProbeTests
{
    [Fact]
    public void Parse_reads_video_and_audio_stream_fields_from_real_ffprobe_json_shape()
    {
        const string json = """
        {
            "streams": [
                {"index":0,"codec_type":"video","codec_name":"h264","width":1920,"height":1080,"r_frame_rate":"30000/1001"},
                {"index":1,"codec_type":"audio","codec_name":"aac","sample_rate":"48000","channels":2}
            ],
            "format": {"duration":"12.345000"}
        }
        """;

        var probe = FfprobeCompositionProbe.Parse(json);

        Assert.NotNull(probe);
        Assert.Equal(1920, probe!.Width);
        Assert.Equal(1080, probe.Height);
        Assert.Equal(TimeSpan.FromSeconds(12.345), probe.Duration);
        Assert.Equal(30000.0 / 1001, probe.FrameRate, 3);
        Assert.Equal("h264", probe.VideoCodec);
        Assert.Equal("aac", probe.AudioCodec);
        Assert.Equal(48000, probe.AudioSampleRate);
        Assert.Equal(2, probe.AudioChannels);
    }

    [Fact]
    public void Parse_returns_null_when_no_video_stream_is_present()
    {
        const string json = """
        {
            "streams": [
                {"index":0,"codec_type":"audio","codec_name":"aac","sample_rate":"48000","channels":2}
            ],
            "format": {"duration":"5.0"}
        }
        """;

        Assert.Null(FfprobeCompositionProbe.Parse(json));
    }

    [Fact]
    public void Parse_returns_null_for_a_video_only_stream_with_no_audio()
    {
        const string json = """
        {
            "streams": [
                {"index":0,"codec_type":"video","codec_name":"h264","width":640,"height":360,"r_frame_rate":"30/1"}
            ],
            "format": {"duration":"5.0"}
        }
        """;

        var probe = FfprobeCompositionProbe.Parse(json);

        Assert.NotNull(probe);
        Assert.Null(probe!.AudioCodec);
    }

    [Fact]
    public void Parse_returns_null_for_malformed_or_empty_output()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => FfprobeCompositionProbe.Parse(string.Empty));
        Assert.Null(FfprobeCompositionProbe.Parse("{}"));
    }
}
