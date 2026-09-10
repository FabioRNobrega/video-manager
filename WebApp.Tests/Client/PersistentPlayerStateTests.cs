using WebApp.Client.Models;
using WebApp.Client.Services;

namespace WebApp.Tests.Client;

public sealed class PersistentPlayerStateTests
{
    [Fact]
    public void Starts_hidden_with_video_stream_base_path()
    {
        var state = new PersistentPlayerState();

        Assert.False(state.HasSelection);
        Assert.Null(state.Selected);
        Assert.Null(state.SelectedId);
        Assert.True(state.CanSaveCut);
        Assert.Equal(PersistentPlayerState.VideoStreamBasePath, state.StreamBasePath);
    }

    [Fact]
    public void Select_video_shows_player_and_allows_cut_export()
    {
        var state = new PersistentPlayerState();
        var video = CreateVideo("video-one");

        state.SelectVideo(video);

        Assert.True(state.HasSelection);
        Assert.Same(video, state.Selected);
        Assert.Equal("video-one", state.SelectedId);
        Assert.True(state.CanSaveCut);
        Assert.Equal(PersistentPlayerState.VideoStreamBasePath, state.StreamBasePath);
    }

    [Fact]
    public void Select_cut_uses_cut_stream_and_disables_cut_export()
    {
        var state = new PersistentPlayerState();
        var cut = CreateVideo("cut-one");

        state.SelectCut(cut);

        Assert.Same(cut, state.Selected);
        Assert.False(state.CanSaveCut);
        Assert.Equal(PersistentPlayerState.CutStreamBasePath, state.StreamBasePath);
    }

    [Fact]
    public void Select_archive_video_uses_category_stream_and_disables_cut_export()
    {
        var state = new PersistentPlayerState();
        var item = new ArchiveItemDto(
            "music-video",
            "song.mp4",
            ArchiveItemKind.File,
            ".mp4",
            2048,
            DateTime.UtcNow,
            true,
            SubtitleState: SubtitleState.Ready,
            SubtitleUrl: "/api/archive/music/items/music-video/subtitle");

        state.SelectArchiveVideo("music", item);

        Assert.True(state.HasSelection);
        Assert.Equal("music-video", state.SelectedId);
        Assert.Equal("song.mp4", state.Selected?.Name);
        Assert.Equal(SubtitleState.Ready, state.Selected?.SubtitleState);
        Assert.Equal("/api/archive/music/items/music-video/subtitle", state.Selected?.SubtitleUrl);
        Assert.Equal("api/archive/music/items", state.StreamBasePath);
        Assert.False(state.CanSaveCut);
    }

    [Fact]
    public void Select_music_uses_audio_mode_and_current_folder_playlist()
    {
        var state = new PersistentPlayerState();
        var first = CreateMusic("song-one", "01.mp3");
        var second = CreateMusic("song-two", "02.wav");

        state.SelectMusic(second, [first, second]);

        Assert.True(state.HasSelection);
        Assert.True(state.IsMusic);
        Assert.Equal(PersistentMediaKind.Music, state.MediaKind);
        Assert.Equal("song-two", state.SelectedId);
        Assert.Equal("api/archive/music/items/song-two", state.StreamBasePath);
        Assert.False(state.CanSaveCut);
        Assert.Equal("/api/archive/music/items/album/cover", state.AlbumCoverUrl);
        Assert.Equal(["song-one", "song-two"], state.Playlist.Select(track => track.Id));
        Assert.True(state.CanSelectPreviousTrack);
        Assert.False(state.CanSelectNextTrack);
        Assert.False(state.CanReturnToPlaylist);
    }

    [Fact]
    public void Select_music_clears_a_stale_playlist_route_reference()
    {
        var state = new PersistentPlayerState();
        var video = CreateArchiveVideo("clip-one", "clip.mp4");
        state.EnterPlaylistView("videos", "folder-1", "My Folder", [video]);
        var first = CreateMusic("song-one", "01.mp3");
        var second = CreateMusic("song-two", "02.wav");

        state.SelectMusic(first, [first, second]);

        Assert.False(state.CanReturnToPlaylist);
        Assert.Null(state.PlaylistCategory);
        Assert.Null(state.PlaylistFolderId);
    }

    [Fact]
    public void Select_music_uses_audio_url_category_for_stream_base_path()
    {
        var state = new PersistentPlayerState();
        var first = CreateMusic("chapter-one", "01.mp3", "books");
        var second = CreateMusic("chapter-two", "02.mp3", "books");

        state.SelectMusic(first, [first, second]);

        Assert.True(state.IsMusic);
        Assert.Equal("api/archive/books/items/chapter-one", state.StreamBasePath);

        Assert.True(state.SelectNextTrack());
        Assert.Equal("chapter-two", state.SelectedId);
        Assert.Equal("api/archive/books/items/chapter-two", state.StreamBasePath);
    }

    [Fact]
    public void Music_previous_and_next_stop_at_playlist_boundaries()
    {
        var state = new PersistentPlayerState();
        var first = CreateMusic("song-one", "01.mp3");
        var second = CreateMusic("song-two", "02.wav");
        state.SelectMusic(first, [first, second]);

        Assert.False(state.SelectPreviousTrack());
        Assert.Equal("song-one", state.SelectedId);

        Assert.True(state.SelectNextTrack());
        Assert.Equal("song-two", state.SelectedId);

        Assert.False(state.SelectNextTrack());
        Assert.Equal("song-two", state.SelectedId);

        Assert.True(state.SelectPreviousTrack());
        Assert.Equal("song-one", state.SelectedId);
    }

    [Fact]
    public void Enter_playlist_view_selects_first_item_and_builds_mixed_queue()
    {
        var state = new PersistentPlayerState();
        var video = CreateArchiveVideo("clip-one", "clip.mp4");
        var music = CreateArchiveMusic("song-one", "song.mp3");

        state.EnterPlaylistView("videos", "folder-1", "My Folder", [video, music]);

        Assert.True(state.PlaylistViewActive);
        Assert.Equal("My Folder", state.PlaylistFolderName);
        Assert.Equal("videos", state.PlaylistCategory);
        Assert.Equal("folder-1", state.PlaylistFolderId);
        Assert.True(state.CanReturnToPlaylist);
        Assert.True(state.HasPlaylist);
        Assert.Equal("clip-one", state.SelectedId);
        Assert.Equal(PersistentMediaKind.Video, state.MediaKind);
        Assert.Equal(["clip-one", "song-one"], state.Playlist.Select(track => track.Id));
        Assert.False(state.CanSelectPreviousTrack);
        Assert.True(state.CanSelectNextTrack);
    }

    [Fact]
    public void Playlist_view_next_track_advances_across_media_kinds()
    {
        var state = new PersistentPlayerState();
        var video = CreateArchiveVideo("clip-one", "clip.mp4");
        var music = CreateArchiveMusic("song-one", "song.mp3");
        state.EnterPlaylistView("videos", "folder-1", "My Folder", [video, music]);

        Assert.True(state.SelectNextTrack());

        Assert.Equal("song-one", state.SelectedId);
        Assert.Equal(PersistentMediaKind.Music, state.MediaKind);
        Assert.True(state.IsMusic);
        Assert.False(state.CanSelectNextTrack);
    }

    [Fact]
    public void Select_playlist_item_jumps_to_the_clicked_queue_entry()
    {
        var state = new PersistentPlayerState();
        var first = CreateArchiveVideo("clip-one", "clip.mp4");
        var second = CreateArchiveVideo("clip-two", "clip2.mp4");
        var third = CreateArchiveMusic("song-one", "song.mp3");
        state.EnterPlaylistView("videos", "folder-1", "My Folder", [first, second, third]);

        Assert.True(state.SelectPlaylistItem("song-one"));

        Assert.Equal("song-one", state.SelectedId);
        Assert.False(state.SelectPlaylistItem("missing-id"));
        Assert.Equal("song-one", state.SelectedId);
    }

    [Fact]
    public void Enter_playlist_view_with_no_playable_items_clears_selection()
    {
        var state = new PersistentPlayerState();
        state.SelectVideo(CreateVideo("video-one"));

        state.EnterPlaylistView("videos", "folder-empty", "Empty Folder", []);

        Assert.True(state.PlaylistViewActive);
        Assert.False(state.HasSelection);
        Assert.False(state.HasPlaylist);
    }

    [Fact]
    public void Exit_playlist_view_clears_flag_but_preserves_selection_and_queue()
    {
        var state = new PersistentPlayerState();
        var video = CreateArchiveVideo("clip-one", "clip.mp4");
        var music = CreateArchiveMusic("song-one", "song.mp3");
        state.EnterPlaylistView("videos", "folder-1", "My Folder", [video, music]);

        state.ExitPlaylistView();

        Assert.False(state.PlaylistViewActive);
        Assert.True(state.HasSelection);
        Assert.True(state.HasPlaylist);
        Assert.Equal("clip-one", state.SelectedId);
        Assert.True(state.CanReturnToPlaylist);
        Assert.Equal("videos", state.PlaylistCategory);
        Assert.Equal("folder-1", state.PlaylistFolderId);
    }

    [Fact]
    public void Selecting_a_single_video_clears_any_active_playlist()
    {
        var state = new PersistentPlayerState();
        var video = CreateArchiveVideo("clip-one", "clip.mp4");
        var music = CreateArchiveMusic("song-one", "song.mp3");
        state.EnterPlaylistView("videos", "folder-1", "My Folder", [video, music]);

        state.SelectVideo(CreateVideo("other-video"));

        Assert.False(state.HasPlaylist);
        Assert.False(state.CanSelectPreviousTrack);
        Assert.False(state.CanSelectNextTrack);
        Assert.False(state.CanReturnToPlaylist);
        Assert.Null(state.PlaylistCategory);
        Assert.Null(state.PlaylistFolderId);
    }

    [Fact]
    public void Update_selected_replaces_current_item_only_when_ids_match()
    {
        var state = new PersistentPlayerState();
        state.SelectVideo(CreateVideo("video-one", "Before"));

        state.UpdateSelected(CreateVideo("video-two", "Ignored"));
        Assert.Equal("Before", state.Selected?.Name);

        state.UpdateSelected(CreateVideo("video-one", "After"));
        Assert.Equal("After", state.Selected?.Name);
    }

    [Fact]
    public void Clear_hides_player_and_restores_video_stream_base_path()
    {
        var state = new PersistentPlayerState();
        state.SelectComposition(CreateVideo("composition-one"));

        state.Clear();

        Assert.False(state.HasSelection);
        Assert.Null(state.Selected);
        Assert.Equal(PersistentPlayerState.VideoStreamBasePath, state.StreamBasePath);
    }

    [Fact]
    public void Selection_and_clear_raise_state_changed()
    {
        var state = new PersistentPlayerState();
        var notifications = 0;
        state.StateChanged += () => notifications++;

        state.SelectVideo(CreateVideo("video-one"));
        state.Clear();

        Assert.Equal(2, notifications);
    }

    [Fact]
    public void Notify_cut_queued_raises_cut_event()
    {
        var state = new PersistentPlayerState();
        var notifications = 0;
        state.CutQueued += () => notifications++;

        state.NotifyCutQueued();

        Assert.Equal(1, notifications);
    }

    private static VideoItemDto CreateVideo(string id, string? name = null) =>
        new(
            id,
            name ?? id,
            ".mp4",
            1024,
            ThumbnailState.Unavailable,
            null,
            HoverPreviewState.Unavailable,
            null,
            SubtitleState.Unavailable,
            null,
            61,
            1920,
            1080);

    private static ArchiveItemDto CreateMusic(string id, string name, string category = "music") =>
        new(
            id,
            name,
            ArchiveItemKind.File,
            Path.GetExtension(name),
            2048,
            DateTime.UtcNow,
            false,
            IsMusic: true,
            AudioUrl: $"/api/archive/{category}/items/{id}/audio",
            AlbumCoverUrl: $"/api/archive/{category}/items/album/cover");

    private static ArchiveItemDto CreateArchiveVideo(string id, string name) =>
        new(
            id,
            name,
            ArchiveItemKind.File,
            Path.GetExtension(name),
            4096,
            DateTime.UtcNow,
            true,
            ThumbnailUrl: $"/api/archive/videos/items/{id}/thumbnail");

    private static ArchiveItemDto CreateArchiveMusic(string id, string name) =>
        CreateMusic(id, name, "videos");
}
