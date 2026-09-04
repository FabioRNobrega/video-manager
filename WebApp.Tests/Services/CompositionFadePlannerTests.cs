using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class CompositionFadePlannerTests
{
    [Fact]
    public void First_clip_fades_out_only()
    {
        var durations = new[] { TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20) };

        var plans = CompositionFadePlanner.Plan(durations, TimeSpan.FromSeconds(5));

        var first = plans[0];
        Assert.Null(first.FadeInStart);
        Assert.Null(first.FadeInDuration);
        Assert.Equal(TimeSpan.FromSeconds(15), first.FadeOutStart);
        Assert.Equal(TimeSpan.FromSeconds(5), first.FadeOutDuration);
    }

    [Fact]
    public void Middle_clip_fades_in_and_out()
    {
        var durations = new[] { TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20) };

        var plans = CompositionFadePlanner.Plan(durations, TimeSpan.FromSeconds(5));

        var middle = plans[1];
        Assert.Equal(TimeSpan.Zero, middle.FadeInStart);
        Assert.Equal(TimeSpan.FromSeconds(5), middle.FadeInDuration);
        Assert.Equal(TimeSpan.FromSeconds(15), middle.FadeOutStart);
        Assert.Equal(TimeSpan.FromSeconds(5), middle.FadeOutDuration);
    }

    [Fact]
    public void Last_clip_fades_in_only()
    {
        var durations = new[] { TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20) };

        var plans = CompositionFadePlanner.Plan(durations, TimeSpan.FromSeconds(5));

        var last = plans[2];
        Assert.Equal(TimeSpan.Zero, last.FadeInStart);
        Assert.Equal(TimeSpan.FromSeconds(5), last.FadeInDuration);
        Assert.Null(last.FadeOutStart);
        Assert.Null(last.FadeOutDuration);
    }

    [Fact]
    public void Two_clip_composition_has_no_overlap_between_boundary_fades()
    {
        var durations = new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10) };

        var plans = CompositionFadePlanner.Plan(durations, TimeSpan.FromSeconds(5));

        Assert.Null(plans[0].FadeInStart);
        Assert.Equal(TimeSpan.FromSeconds(5), plans[0].FadeOutStart);
        Assert.Equal(TimeSpan.Zero, plans[1].FadeInStart);
        Assert.Null(plans[1].FadeOutStart);
    }

    [Fact]
    public void A_clip_shorter_than_twice_the_transition_duration_gets_a_clamped_fade()
    {
        var durations = new[] { TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(800), TimeSpan.FromSeconds(20) };

        var plans = CompositionFadePlanner.Plan(durations, TimeSpan.FromSeconds(5));

        var middle = plans[1];
        Assert.Equal(TimeSpan.FromMilliseconds(400), middle.FadeInDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(400), middle.FadeOutDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(400), middle.FadeOutStart);
    }

    [Fact]
    public void A_clip_of_exactly_twice_the_transition_duration_gets_unclamped_full_length_fades()
    {
        var durations = new[] { TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20) };

        var plans = CompositionFadePlanner.Plan(durations, TimeSpan.FromSeconds(5));

        var middle = plans[1];
        Assert.Equal(TimeSpan.FromSeconds(5), middle.FadeInDuration);
        Assert.Equal(TimeSpan.FromSeconds(5), middle.FadeOutDuration);
        Assert.Equal(TimeSpan.FromSeconds(5), middle.FadeOutStart);
    }
}
