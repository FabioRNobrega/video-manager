using WebApp.Client.Models;

namespace WebApp.Client.Services;

public sealed class PersistentPlayerState
{
    public const string VideoStreamBasePath = "api/videos";
    public const string CutStreamBasePath = "api/cuts";
    public const string CompositionStreamBasePath = "api/compositions";

    public event Action? StateChanged;
    public event Action? CutQueued;

    public VideoItemDto? Selected { get; private set; }
    public string StreamBasePath { get; private set; } = VideoStreamBasePath;
    public bool HasSelection => Selected is not null;
    public bool CanSaveCut => string.Equals(StreamBasePath, VideoStreamBasePath, StringComparison.Ordinal);
    public string? SelectedId => Selected?.Id;

    public void SelectVideo(VideoItemDto video) => Select(video, VideoStreamBasePath);

    public void SelectCut(VideoItemDto cut) => Select(cut, CutStreamBasePath);

    public void SelectComposition(VideoItemDto composition) => Select(composition, CompositionStreamBasePath);

    public void SelectArchiveVideo(string category, ArchiveItemDto item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(item);

        Select(
            new VideoItemDto(
                item.Id,
                item.Name,
                item.Extension ?? string.Empty,
                item.SizeBytes ?? 0,
                ThumbnailState.Unavailable,
                null,
                HoverPreviewState.Unavailable,
                null,
                null,
                null,
                null),
            $"api/archive/{Uri.EscapeDataString(category)}/items");
    }

    public void Select(VideoItemDto item, string streamBasePath)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(streamBasePath))
        {
            throw new ArgumentException("A stream base path is required.", nameof(streamBasePath));
        }

        Selected = item;
        StreamBasePath = streamBasePath;
        NotifyStateChanged();
    }

    public void UpdateSelected(VideoItemDto item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!string.Equals(Selected?.Id, item.Id, StringComparison.Ordinal))
        {
            return;
        }

        Selected = item;
        NotifyStateChanged();
    }

    public void Clear()
    {
        Selected = null;
        StreamBasePath = VideoStreamBasePath;
        NotifyStateChanged();
    }

    public void NotifyCutQueued() => CutQueued?.Invoke();

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
