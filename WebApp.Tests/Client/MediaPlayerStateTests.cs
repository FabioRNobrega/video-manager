using WebApp.Client.Models;

namespace WebApp.Tests.Client;

public sealed class MediaPlayerStateTests
{
    [Fact]
    public void Subtitles_default_to_enabled()
    {
        var state = new MediaPlayerState();

        Assert.True(state.IsSubtitlesEnabled);
    }

    [Fact]
    public void Select_resets_subtitles_to_enabled_for_new_selection()
    {
        var state = new MediaPlayerState();
        state.Select("one");
        state.SetSubtitlesEnabled(false);

        state.Select("two");

        Assert.True(state.IsSubtitlesEnabled);
    }

    [Fact]
    public void Set_subtitles_enabled_does_not_change_unrelated_state()
    {
        var state = new MediaPlayerState();
        state.Synchronize(new MediaSnapshot(12, 60, 0.4, true, 1.25, false, true, false));

        state.SetSubtitlesEnabled(false);

        Assert.False(state.IsSubtitlesEnabled);
        Assert.True(state.IsMuted);
        Assert.Equal(0.4, state.Volume);
        Assert.Equal(1.25, state.PlaybackRate);
        Assert.Equal(12, state.CurrentTime);
    }
}
