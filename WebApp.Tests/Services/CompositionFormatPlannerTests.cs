using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class CompositionFormatPlannerTests
{
    [Fact]
    public void Smallest_pixel_count_input_wins_the_canvas()
    {
        var probes = new List<CompositionInputProbe>
        {
            CreateProbe(1920, 1080, 30),
            CreateProbe(640, 360, 30),
            CreateProbe(1280, 720, 30),
        };

        var plan = CompositionFormatPlanner.Plan(probes);

        Assert.Equal(640, plan.Width);
        Assert.Equal(360, plan.Height);
    }

    [Fact]
    public void A_tie_on_pixel_count_is_resolved_by_first_in_order()
    {
        var probes = new List<CompositionInputProbe>
        {
            CreateProbe(1280, 720, 30),
            CreateProbe(720, 1280, 30),
        };

        var plan = CompositionFormatPlanner.Plan(probes);

        Assert.Equal(1280, plan.Width);
        Assert.Equal(720, plan.Height);
    }

    [Fact]
    public void Target_frame_rate_is_the_minimum_of_all_inputs()
    {
        var probes = new List<CompositionInputProbe>
        {
            CreateProbe(640, 360, 60),
            CreateProbe(640, 360, 24),
            CreateProbe(640, 360, 30),
        };

        var plan = CompositionFormatPlanner.Plan(probes);

        Assert.Equal(24, plan.FrameRate);
    }

    [Fact]
    public void Audio_format_is_always_the_fixed_target_regardless_of_input_mix()
    {
        var probes = new List<CompositionInputProbe>
        {
            new(640, 360, TimeSpan.FromSeconds(10), 30, "h264", "aac", 44100, 1),
            new(640, 360, TimeSpan.FromSeconds(10), 30, "h264", "mp3", 22050, 6),
        };

        var plan = CompositionFormatPlanner.Plan(probes);

        Assert.Equal(48000, plan.AudioSampleRate);
        Assert.Equal(2, plan.AudioChannels);
    }

    private static CompositionInputProbe CreateProbe(int width, int height, double frameRate) =>
        new(width, height, TimeSpan.FromSeconds(10), frameRate, "h264", "aac", 48000, 2);
}
