namespace WebApp.Client.Models;

public sealed class FillTabState
{
    public string? SelectionId { get; private set; }
    public bool IsActive { get; private set; }

    public bool Select(string? id)
    {
        if (string.Equals(SelectionId, id, StringComparison.Ordinal))
        {
            return false;
        }

        SelectionId = id;
        var wasActive = IsActive;
        IsActive = false;
        return wasActive;
    }

    public bool Enter()
    {
        if (SelectionId is null || IsActive)
        {
            return false;
        }

        IsActive = true;
        return true;
    }

    public bool Exit()
    {
        if (!IsActive)
        {
            return false;
        }

        IsActive = false;
        return true;
    }
}
