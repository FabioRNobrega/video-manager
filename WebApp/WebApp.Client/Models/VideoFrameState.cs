namespace WebApp.Client.Models;

public sealed class VideoFrameState
{
    public double PositionX { get; private set; } = 50;
    public double PositionY { get; private set; } = 50;
    public string? SelectionId { get; private set; }

    public void Select(string? id)
    {
        if (!string.Equals(SelectionId, id, StringComparison.Ordinal))
        {
            SelectionId = id;
            Reset();
        }
    }

    public void Reset()
    {
        PositionX = 50;
        PositionY = 50;
    }

    public void ApplyDrag(double startingPositionX, double startingPositionY, double deltaX, double deltaY,
        double viewportWidth, double viewportHeight, double videoWidth, double videoHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0 || videoWidth <= 0 || videoHeight <= 0)
        {
            return;
        }

        var scale = Math.Max(viewportWidth / videoWidth, viewportHeight / videoHeight);
        var overflowX = Math.Max(0, (videoWidth * scale) - viewportWidth);
        var overflowY = Math.Max(0, (videoHeight * scale) - viewportHeight);

        PositionX = overflowX > 0
            ? Math.Clamp(startingPositionX - (deltaX / overflowX * 100), 0, 100)
            : startingPositionX;
        PositionY = overflowY > 0
            ? Math.Clamp(startingPositionY - (deltaY / overflowY * 100), 0, 100)
            : startingPositionY;
    }
}
