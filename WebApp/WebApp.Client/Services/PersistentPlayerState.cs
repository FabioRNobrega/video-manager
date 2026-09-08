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
    public PersistentMediaKind MediaKind { get; private set; }
    public IReadOnlyList<MusicTrackDto> MusicPlaylist { get; private set; } = [];
    public string? AlbumCoverUrl { get; private set; }
    public string StreamBasePath { get; private set; } = VideoStreamBasePath;
    public bool HasSelection => Selected is not null;
    public bool CanSaveCut => MediaKind == PersistentMediaKind.Video &&
        string.Equals(StreamBasePath, VideoStreamBasePath, StringComparison.Ordinal);
    public string? SelectedId => Selected?.Id;
    public bool IsMusic => MediaKind == PersistentMediaKind.Music;
    public bool CanSelectPreviousTrack => IsMusic && CurrentMusicIndex > 0;
    public bool CanSelectNextTrack => IsMusic &&
        CurrentMusicIndex >= 0 &&
        CurrentMusicIndex < MusicPlaylist.Count - 1;

    private int CurrentMusicIndex => SelectedId is null
        ? -1
        : MusicPlaylist.ToList().FindIndex(track => string.Equals(track.Id, SelectedId, StringComparison.Ordinal));

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
                item.SubtitleState,
                item.SubtitleUrl,
                null,
                null,
                null),
            $"api/archive/{Uri.EscapeDataString(category)}/items");
    }

    public void SelectMusic(ArchiveItemDto item, IReadOnlyList<ArchiveItemDto> currentFolderItems)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(currentFolderItems);
        if (!item.IsMusic || string.IsNullOrWhiteSpace(item.AudioUrl))
        {
            throw new ArgumentException("A playable music item is required.", nameof(item));
        }

        var playlist = currentFolderItems
            .Where(track => track.IsMusic && !string.IsNullOrWhiteSpace(track.AudioUrl))
            .Select(ToMusicTrack)
            .ToList();
        if (!playlist.Any(track => string.Equals(track.Id, item.Id, StringComparison.Ordinal)))
        {
            playlist.Add(ToMusicTrack(item));
        }

        SelectMusic(ToMusicTrack(item), playlist);
    }

    public bool SelectPreviousTrack()
    {
        if (!CanSelectPreviousTrack)
        {
            return false;
        }

        SelectMusic(MusicPlaylist[CurrentMusicIndex - 1], MusicPlaylist);
        return true;
    }

    public bool SelectNextTrack()
    {
        if (!CanSelectNextTrack)
        {
            return false;
        }

        SelectMusic(MusicPlaylist[CurrentMusicIndex + 1], MusicPlaylist);
        return true;
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
        MediaKind = PersistentMediaKind.Video;
        MusicPlaylist = [];
        AlbumCoverUrl = null;
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
        MediaKind = PersistentMediaKind.Video;
        MusicPlaylist = [];
        AlbumCoverUrl = null;
        NotifyStateChanged();
    }

    public void NotifyCutQueued() => CutQueued?.Invoke();

    private void NotifyStateChanged() => StateChanged?.Invoke();

    private void SelectMusic(MusicTrackDto track, IReadOnlyList<MusicTrackDto> playlist)
    {
        Selected = new VideoItemDto(
            track.Id,
            track.Name,
            track.Extension,
            track.SizeBytes,
            ThumbnailState.Unavailable,
            null,
            HoverPreviewState.Unavailable,
            null,
            SubtitleState.Unavailable,
            null,
            track.DurationSeconds,
            null,
            null);
        StreamBasePath = track.StreamBasePath;
        MediaKind = PersistentMediaKind.Music;
        MusicPlaylist = playlist.ToList();
        AlbumCoverUrl = track.AlbumCoverUrl;
        NotifyStateChanged();
    }

    private static MusicTrackDto ToMusicTrack(ArchiveItemDto item) =>
        new(
            item.Id,
            item.Name,
            item.Extension ?? string.Empty,
            item.SizeBytes ?? 0,
            item.AudioUrl ?? string.Empty,
            item.AlbumCoverUrl,
            item.DurationSeconds,
            GetStreamBasePath(item.AudioUrl));

    private static string GetStreamBasePath(string? audioUrl)
    {
        const string audioSuffix = "/audio";
        if (string.IsNullOrWhiteSpace(audioUrl))
        {
            return VideoStreamBasePath;
        }

        var path = audioUrl.StartsWith("/", StringComparison.Ordinal) ? audioUrl[1..] : audioUrl;
        return path.EndsWith(audioSuffix, StringComparison.OrdinalIgnoreCase)
            ? path[..^audioSuffix.Length]
            : path;
    }
}
