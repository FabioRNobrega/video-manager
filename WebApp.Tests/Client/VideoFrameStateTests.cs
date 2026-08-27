using WebApp.Client.Models;

namespace WebApp.Tests.Client;

public sealed class VideoFrameStateTests
{
    [Fact]
    public void Starts_centered_and_reset_returns_to_center()
    {
        var state = new VideoFrameState();
        state.ApplyDrag(50, 50, -100, 0, 100, 200, 400, 200);

        state.Reset();

        Assert.Equal(50, state.PositionX);
        Assert.Equal(50, state.PositionY);
    }

    [Fact]
    public void Horizontal_overflow_converts_drag_to_normalized_position_and_locks_vertical_axis()
    {
        var state = new VideoFrameState();

        state.ApplyDrag(50, 50, 50, 100, 90, 160, 160, 90);

        Assert.True(state.PositionX < 50);
        Assert.Equal(50, state.PositionY);
    }

    [Fact]
    public void Vertical_overflow_converts_both_drag_directions_and_locks_horizontal_axis()
    {
        var state = new VideoFrameState();
        state.ApplyDrag(50, 50, 100, -20, 90, 160, 90, 300);
        var movedUp = state.PositionY;
        state.ApplyDrag(50, 50, 100, 20, 90, 160, 90, 300);

        Assert.True(movedUp > 50);
        Assert.True(state.PositionY < 50);
        Assert.Equal(50, state.PositionX);
    }

    [Theory]
    [InlineData(-10000, 100)]
    [InlineData(10000, 0)]
    public void Drag_is_clamped_at_both_bounds(double deltaX, double expected)
    {
        var state = new VideoFrameState();

        state.ApplyDrag(50, 50, deltaX, 0, 90, 160, 160, 90);

        Assert.Equal(expected, state.PositionX);
    }

    [Fact]
    public void Changing_selection_discards_framing_and_reselecting_also_centers()
    {
        var state = new VideoFrameState();
        state.Select("first");
        state.ApplyDrag(50, 50, -100, 0, 90, 160, 160, 90);
        Assert.NotEqual(50, state.PositionX);

        state.Select("second");
        Assert.Equal(50, state.PositionX);
        Assert.Equal(50, state.PositionY);

        state.ApplyDrag(50, 50, 100, 0, 90, 160, 160, 90);
        state.Select("first");
        Assert.Equal(50, state.PositionX);
        Assert.Equal(50, state.PositionY);
    }

    [Fact]
    public void Invalid_geometry_does_not_change_state()
    {
        var state = new VideoFrameState();

        state.ApplyDrag(50, 50, 10, 10, 0, 160, 160, 90);

        Assert.Equal(50, state.PositionX);
        Assert.Equal(50, state.PositionY);
    }
}
