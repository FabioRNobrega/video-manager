using WebApp.Client.Models;

namespace WebApp.Tests.Client;

public sealed class FillTabStateTests
{
    [Fact]
    public void Enter_requires_a_selected_video()
    {
        var state = new FillTabState();

        var entered = state.Enter();

        Assert.False(entered);
        Assert.False(state.IsActive);
    }

    [Fact]
    public void Selected_video_can_enter_and_exit_fill_tab()
    {
        var state = new FillTabState();
        state.Select("video-one");

        var entered = state.Enter();
        var exited = state.Exit();

        Assert.True(entered);
        Assert.True(exited);
        Assert.False(state.IsActive);
    }

    [Fact]
    public void Repeated_exit_is_harmless()
    {
        var state = new FillTabState();
        state.Select("video-one");
        state.Enter();
        state.Exit();

        var exitedAgain = state.Exit();

        Assert.False(exitedAgain);
        Assert.False(state.IsActive);
    }

    [Theory]
    [InlineData("video-two")]
    [InlineData(null)]
    public void Changing_or_clearing_selection_exits_fill_tab(string? nextSelection)
    {
        var state = new FillTabState();
        state.Select("video-one");
        state.Enter();

        var exitedActiveMode = state.Select(nextSelection);

        Assert.True(exitedActiveMode);
        Assert.False(state.IsActive);
        Assert.Equal(nextSelection, state.SelectionId);
    }
}
