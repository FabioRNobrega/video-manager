using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebApp.Client.Models;
using WebApp.Configuration;
using WebApp.Services;

namespace WebApp.Tests.Endpoints;

public sealed class ArchiveEndpointsTests
{
    [Fact]
    public async Task Listing_returns_browser_safe_category_items()
    {
        using var root = CreateArchive();
        Directory.CreateDirectory(Path.Combine(root.Path, "Pictures", "Trips"));
        await File.WriteAllTextAsync(Path.Combine(root.Path, "Pictures", "photo.jpg"), "content");
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/archive/photos/items");
        var json = await response.Content.ReadAsStringAsync();
        var listing = await response.Content.ReadFromJsonAsync<ArchiveListingDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(root.Path, json);
        Assert.Equal("Photos", listing!.DisplayName);
        Assert.Contains(listing.Items, item => item.Name == "Trips" && item.Kind == ArchiveItemKind.Folder);
        Assert.Contains(listing.Items, item => item.Name == "photo.jpg" && item.Kind == ArchiveItemKind.File);
    }

    [Fact]
    public async Task Listing_returns_archive_video_preview_contract_for_all_categories()
    {
        using var root = CreateArchive();
        var videoPath = Path.Combine(root.Path, "Downloads", "clip.mp4");
        var documentPath = Path.Combine(root.Path, "Downloads", "note.txt");
        byte[] videoFixture = [10, 20, 30, 40, 50];
        await File.WriteAllBytesAsync(videoPath, videoFixture);
        await File.WriteAllTextAsync(documentPath, "content");
        using var factory = new VideoManagerFactory(root.Path, hoverPreviewEnabled: true);
        using var client = factory.CreateClient();

        var pendingListing = (await client.GetFromJsonAsync<ArchiveListingDto>("/api/archive/downloads/items"))!;
        var pendingVideo = pendingListing.Items.Single(item => item.Name == "clip.mp4");
        var nonVideo = pendingListing.Items.Single(item => item.Name == "note.txt");

        Assert.True(pendingVideo.IsVideo);
        Assert.Equal(ThumbnailState.Pending, pendingVideo.ThumbnailState);
        Assert.Null(pendingVideo.ThumbnailUrl);
        Assert.Equal(HoverPreviewState.Pending, pendingVideo.HoverPreviewState);
        Assert.Null(pendingVideo.HoverPreviewUrl);
        Assert.Equal(SubtitleState.Unavailable, pendingVideo.SubtitleState);
        Assert.Null(pendingVideo.SubtitleUrl);
        Assert.Equal(305, pendingVideo.DurationSeconds);
        Assert.Equal(1920, pendingVideo.Width);
        Assert.Equal(1080, pendingVideo.Height);
        Assert.False(nonVideo.IsVideo);
        Assert.Equal(ThumbnailState.Unavailable, nonVideo.ThumbnailState);
        Assert.Null(nonVideo.ThumbnailUrl);
        Assert.Equal(HoverPreviewState.Unavailable, nonVideo.HoverPreviewState);
        Assert.Null(nonVideo.HoverPreviewUrl);
        Assert.Equal(SubtitleState.Unavailable, nonVideo.SubtitleState);
        Assert.Null(nonVideo.SubtitleUrl);
        Assert.Equal(".txt", nonVideo.Extension);

        var thumbnailCache = new ThumbnailCache(Options.Create(new ThumbnailCacheOptions { Path = factory.PreviewPath }));
        var hoverCache = new HoverPreviewCache(Options.Create(new ThumbnailCacheOptions { Path = factory.PreviewPath }));
        var timestamp = File.GetLastWriteTimeUtc(videoPath);
        var relativeIdentity = $"archive/downloads/{pendingVideo.Id}/clip.mp4";
        await File.WriteAllBytesAsync(thumbnailCache.GetFinalPath(thumbnailCache.ComputeKey(relativeIdentity, videoFixture.Length, timestamp)), [1, 2, 3]);
        await File.WriteAllBytesAsync(hoverCache.GetFinalPath(hoverCache.ComputeKey(relativeIdentity, videoFixture.Length, timestamp)), videoFixture);

        using var readyResponse = await client.GetAsync("/api/archive/downloads/items");
        var json = await readyResponse.Content.ReadAsStringAsync();
        var readyListing = await readyResponse.Content.ReadFromJsonAsync<ArchiveListingDto>();
        var readyVideo = readyListing!.Items.Single(item => item.Name == "clip.mp4");

        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        Assert.DoesNotContain(root.Path, json);
        Assert.Equal(ThumbnailState.Ready, readyVideo.ThumbnailState);
        Assert.Equal(HoverPreviewState.Ready, readyVideo.HoverPreviewState);
        Assert.StartsWith("/api/archive/downloads/items/", readyVideo.ThumbnailUrl);
        Assert.StartsWith("/api/archive/downloads/items/", readyVideo.HoverPreviewUrl);

        using var thumbnailResponse = await client.GetAsync(readyVideo.ThumbnailUrl);
        Assert.Equal(HttpStatusCode.OK, thumbnailResponse.StatusCode);
        Assert.Equal("image/jpeg", thumbnailResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, await thumbnailResponse.Content.ReadAsByteArrayAsync());

        using var previewResponse = await client.GetAsync(readyVideo.HoverPreviewUrl);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.Equal("video/mp4", previewResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(videoFixture, await previewResponse.Content.ReadAsByteArrayAsync());

        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, readyVideo.HoverPreviewUrl);
        rangeRequest.Headers.Range = new RangeHeaderValue(1, 3);
        using var rangeResponse = await client.SendAsync(rangeRequest);
        Assert.Equal(HttpStatusCode.PartialContent, rangeResponse.StatusCode);
        Assert.Equal(videoFixture[1..4], await rangeResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Archive_subtitle_endpoint_serves_ready_vtt_and_listing_reports_state()
    {
        using var root = CreateArchive();
        var videoPath = Path.Combine(root.Path, "Downloads", "clip.mp4");
        var subtitlePath = Path.Combine(root.Path, "Downloads", "clip.srt");
        await File.WriteAllBytesAsync(videoPath, [1, 2, 3]);
        await File.WriteAllTextAsync(subtitlePath, "subtitle");
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();

        var pendingListing = (await client.GetFromJsonAsync<ArchiveListingDto>("/api/archive/downloads/items"))!;
        var pending = pendingListing.Items.Single(item => item.Name == "clip.mp4");

        Assert.Equal(SubtitleState.Pending, pending.SubtitleState);
        Assert.Null(pending.SubtitleUrl);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/archive/downloads/items/{pending.Id}/subtitle")).StatusCode);

        var cache = new SubtitleCache(Options.Create(new ThumbnailCacheOptions { Path = factory.PreviewPath }));
        var relativeIdentity = $"archive/downloads/{pending.Id}/clip.mp4";
        var entry = new WebApp.Models.VideoFileEntry(
            pending.Id,
            videoPath,
            relativeIdentity,
            "clip.mp4",
            ".mp4",
            3,
            File.GetLastWriteTimeUtc(videoPath));
        var subtitle = new WebApp.Models.SubtitleFileInfo(
            subtitlePath,
            new FileInfo(subtitlePath).Length,
            File.GetLastWriteTimeUtc(subtitlePath));
        await File.WriteAllTextAsync(cache.GetFinalPath(cache.ComputeKey(entry, subtitle)), "WEBVTT\n\n");

        var readyListing = (await client.GetFromJsonAsync<ArchiveListingDto>("/api/archive/downloads/items"))!;
        var ready = readyListing.Items.Single(item => item.Name == "clip.mp4");

        Assert.Equal(SubtitleState.Ready, ready.SubtitleState);
        Assert.StartsWith("/api/archive/downloads/items/", ready.SubtitleUrl);

        using var subtitleResponse = await client.GetAsync(ready.SubtitleUrl);
        Assert.Equal(HttpStatusCode.OK, subtitleResponse.StatusCode);
        Assert.Equal("text/vtt", subtitleResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("WEBVTT\n\n", await subtitleResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Archive_media_endpoints_reject_non_video_and_stale_ids()
    {
        using var root = CreateArchive();
        await File.WriteAllTextAsync(Path.Combine(root.Path, "Documents", "note.txt"), "content");
        using var factory = new VideoManagerFactory(root.Path, hoverPreviewEnabled: true);
        using var client = factory.CreateClient();
        var listing = (await client.GetFromJsonAsync<ArchiveListingDto>("/api/archive/documents/items"))!;
        var item = Assert.Single(listing.Items);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/archive/documents/items/{item.Id}/thumbnail")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/archive/documents/items/{item.Id}/preview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/archive/documents/items/{item.Id}/subtitle")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/archive/documents/items/{Guid.NewGuid():N}/thumbnail")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/archive/documents/items/%2Fetc%2Fpasswd/preview")).StatusCode);
    }

    [Fact]
    public async Task CreateFolder_rejects_trash_and_accepts_valid_category()
    {
        using var root = CreateArchive();
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();

        using var created = await client.PostAsJsonAsync("/api/archive/documents/folders", new CreateFolderRequest(null, "Projects"));
        using var rejected = await client.PostAsJsonAsync("/api/archive/trash/folders", new CreateFolderRequest(null, "Nope"));

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.True(Directory.Exists(Path.Combine(root.Path, "Documents", "Projects")));
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
    }

    [Fact]
    public async Task Delete_moves_item_to_trash()
    {
        using var root = CreateArchive();
        await File.WriteAllTextAsync(Path.Combine(root.Path, "Documents", "note.txt"), "content");
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();
        var listing = (await client.GetFromJsonAsync<ArchiveListingDto>("/api/archive/documents/items"))!;
        var item = Assert.Single(listing.Items);

        using var response = await client.DeleteAsync($"/api/archive/documents/items/{item.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(File.Exists(Path.Combine(root.Path, "Trash", "note.txt")));
    }

    [Fact]
    public async Task Stream_video_serves_supported_archive_file_without_exposing_paths()
    {
        using var root = CreateArchive();
        await File.WriteAllTextAsync(Path.Combine(root.Path, "Music", "clip.mp4"), "fake mp4");
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();
        var listing = (await client.GetFromJsonAsync<ArchiveListingDto>("/api/archive/music/items"))!;
        var item = Assert.Single(listing.Items);

        using var response = await client.GetAsync($"/api/archive/music/items/{item.Id}/stream");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("video/mp4", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(root.Path, body);
    }

    [Fact]
    public async Task Stream_video_rejects_non_video_archive_file()
    {
        using var root = CreateArchive();
        await File.WriteAllTextAsync(Path.Combine(root.Path, "Documents", "note.txt"), "content");
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();
        var listing = (await client.GetFromJsonAsync<ArchiveListingDto>("/api/archive/documents/items"))!;
        var item = Assert.Single(listing.Items);

        using var response = await client.GetAsync($"/api/archive/documents/items/{item.Id}/stream");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Music_listing_returns_audio_and_cover_contract_without_paths()
    {
        using var root = CreateArchive();
        var album = Path.Combine(root.Path, "Music", "Album");
        Directory.CreateDirectory(album);
        await File.WriteAllBytesAsync(Path.Combine(album, "02.wav"), [1, 2, 3, 4, 5]);
        await File.WriteAllBytesAsync(Path.Combine(album, "01.mp3"), [6, 7, 8, 9, 10]);
        await File.WriteAllBytesAsync(Path.Combine(album, "cover.jpg"), [11, 12, 13]);
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();
        var rootListing = (await client.GetFromJsonAsync<ArchiveListingDto>("/api/archive/music/items"))!;
        var folder = rootListing.Items.Single(item => item.Name == "Album");

        using var response = await client.GetAsync($"/api/archive/music/items?folderId={folder.Id}");
        var json = await response.Content.ReadAsStringAsync();
        var listing = await response.Content.ReadFromJsonAsync<ArchiveListingDto>();
        var tracks = listing!.Items.Where(item => item.IsMusic).ToList();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(root.Path, json);
        Assert.Equal(["01.mp3", "02.wav"], tracks.Select(track => track.Name));
        Assert.All(tracks, track =>
        {
            Assert.False(track.IsVideo);
            Assert.StartsWith("/api/archive/music/items/", track.AudioUrl);
            Assert.EndsWith("/audio", track.AudioUrl);
            Assert.Equal($"/api/archive/music/items/{folder.Id}/cover", track.AlbumCoverUrl);
            Assert.Equal(305, track.DurationSeconds);
        });
    }

    [Fact]
    public async Task Audio_endpoint_streams_music_with_range_processing()
    {
        using var root = CreateArchive();
        byte[] fixture = [10, 20, 30, 40, 50];
        await File.WriteAllBytesAsync(Path.Combine(root.Path, "Music", "song.mp3"), fixture);
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();
        var listing = (await client.GetFromJsonAsync<ArchiveListingDto>("/api/archive/music/items"))!;
        var item = Assert.Single(listing.Items);

        using var response = await client.GetAsync(item.AudioUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/mpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(fixture, await response.Content.ReadAsByteArrayAsync());

        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, item.AudioUrl);
        rangeRequest.Headers.Range = new RangeHeaderValue(1, 3);
        using var rangeResponse = await client.SendAsync(rangeRequest);
        Assert.Equal(HttpStatusCode.PartialContent, rangeResponse.StatusCode);
        Assert.Equal(fixture[1..4], await rangeResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Audio_listing_and_endpoint_work_outside_music_category()
    {
        using var root = CreateArchive();
        var album = Path.Combine(root.Path, "Books", "Audiobook");
        Directory.CreateDirectory(album);
        byte[] fixture = [42, 43, 44, 45, 46];
        await File.WriteAllBytesAsync(Path.Combine(album, "chapter-01.mp3"), fixture);
        await File.WriteAllBytesAsync(Path.Combine(album, "cover.png"), [1, 2, 3]);
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();
        var rootListing = (await client.GetFromJsonAsync<ArchiveListingDto>("/api/archive/books/items"))!;
        var folder = rootListing.Items.Single(item => item.Name == "Audiobook");

        var listing = (await client.GetFromJsonAsync<ArchiveListingDto>($"/api/archive/books/items?folderId={folder.Id}"))!;
        var track = Assert.Single(listing.Items, item => item.IsMusic);

        Assert.True(track.IsMusic);
        Assert.False(track.IsVideo);
        Assert.Equal($"/api/archive/books/items/{track.Id}/audio", track.AudioUrl);
        Assert.Equal($"/api/archive/books/items/{folder.Id}/cover", track.AlbumCoverUrl);

        using var audioResponse = await client.GetAsync(track.AudioUrl);
        Assert.Equal(HttpStatusCode.OK, audioResponse.StatusCode);
        Assert.Equal("audio/mpeg", audioResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(fixture, await audioResponse.Content.ReadAsByteArrayAsync());

        using var coverResponse = await client.GetAsync(track.AlbumCoverUrl);
        Assert.Equal(HttpStatusCode.OK, coverResponse.StatusCode);
        Assert.Equal("image/png", coverResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Cover_endpoint_serves_first_image_and_rejects_invalid_ids()
    {
        using var root = CreateArchive();
        var album = Path.Combine(root.Path, "Music", "Album");
        Directory.CreateDirectory(album);
        await File.WriteAllBytesAsync(Path.Combine(album, "song.wav"), [1, 2, 3]);
        await File.WriteAllBytesAsync(Path.Combine(album, "zeta.png"), [4, 5, 6]);
        await File.WriteAllBytesAsync(Path.Combine(album, "alpha.jpeg"), [7, 8, 9]);
        await File.WriteAllBytesAsync(Path.Combine(root.Path, "Documents", "cover.jpg"), [10]);
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();
        var rootListing = (await client.GetFromJsonAsync<ArchiveListingDto>("/api/archive/music/items"))!;
        var folder = rootListing.Items.Single(item => item.Name == "Album");
        var listing = (await client.GetFromJsonAsync<ArchiveListingDto>($"/api/archive/music/items?folderId={folder.Id}"))!;
        var track = listing.Items.Single(item => item.IsMusic);

        using var response = await client.GetAsync(track.AlbumCoverUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 7, 8, 9 }, await response.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/archive/music/items/{track.Id}/cover")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/archive/documents/items/{folder.Id}/cover")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/archive/music/items/%2Fetc%2Fpasswd/cover")).StatusCode);
    }

    [Fact]
    public void Archive_browser_markup_uses_unified_dropdown_cards_and_keeps_video_grid_specialized()
    {
        var archiveBrowser = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../WebApp/WebApp.Client/Components/ArchiveBrowser.razor"));
        var home = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../WebApp/WebApp.Client/Pages/UtilitiesPages/VideoComposition.razor"));

        Assert.Contains("bi-three-dots-vertical", archiveBrowser);
        Assert.Contains("data-bs-toggle=\"dropdown\"", archiveBrowser);
        Assert.Contains("dropdown-menu dropdown-menu-end", archiveBrowser);
        Assert.Contains("ratio ratio-16x9", archiveBrowser);
        Assert.Contains("HoverPreviewUrl", archiveBrowser);
        Assert.Contains("AlbumCoverUrl", archiveBrowser);
        Assert.Contains("archive-music-cover ratio ratio-1x1", archiveBrowser);
        Assert.Contains("archive-music-overlay", archiveBrowser);
        Assert.Contains("FormatDisplayName(item)", archiveBrowser);
        Assert.Contains("FormatDuration(item.DurationSeconds)", archiveBrowser);
        Assert.Contains("SelectMusic", archiveBrowser);
        Assert.Contains("\"pdf\"", archiveBrowser);
        Assert.Contains("\"mp4\"", archiveBrowser);
        Assert.Contains("bi-filetype-{type}", archiveBrowser);
        Assert.Contains("FormatDuration(item.DurationSeconds)", archiveBrowser);
        Assert.Contains("FormatResolution(item.Width, item.Height)", archiveBrowser);
        Assert.Contains("FormatDimensions(item.Width, item.Height)", archiveBrowser);
        Assert.DoesNotContain("card-footer d-flex gap-2 justify-content-center", archiveBrowser);
        Assert.Contains("<VideoGrid Items=\"_cuts\"", home);
        Assert.Contains("<VideoGrid Items=\"_compositions\"", home);

        var player = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../WebApp/WebApp.Client/Components/Player.razor"));
        var controls = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../WebApp/WebApp.Client/Components/MediaPlayerControls.razor"));

        Assert.Contains("<audio @key=\"Selected.Id\"", player);
        Assert.Contains("music-cover-stage", player);
        Assert.Contains("IsMusicMode=\"PlayerState.IsMusic\"", player);
        Assert.Contains("bi-chevron-compact-left", controls);
        Assert.Contains("bi-chevron-compact-right", controls);
        Assert.Contains("@if (!IsMusicMode)", controls);
    }

    private sealed class VideoManagerFactory : WebApplicationFactory<Program>
    {
        private readonly string _archiveRoot;
        private readonly bool _hoverPreviewEnabled;
        private readonly string _previewPath = CreateDirectory();

        public VideoManagerFactory(string archiveRoot, bool hoverPreviewEnabled = false)
        {
            _archiveRoot = archiveRoot;
            _hoverPreviewEnabled = hoverPreviewEnabled;
        }

        public string PreviewPath => _previewPath;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ArchiveRoot:Path"] = _archiveRoot,
                    ["VideoLibrary:Path"] = Path.Combine(_archiveRoot, "Videos"),
                    ["ThumbnailCache:Path"] = _previewPath,
                    ["VideoCut:Path"] = Path.Combine(_archiveRoot, "Videos", "Cuts"),
                    ["VideoComposition:Path"] = Path.Combine(_archiveRoot, "Videos", "VideoComposition"),
                    ["HoverPreview:Enabled"] = _hoverPreviewEnabled.ToString()
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IVideoDurationProbe>();
                services.RemoveAll<IVideoResolutionProbe>();
                services.AddSingleton<IVideoDurationProbe>(new FixedDurationProbe(TimeSpan.FromSeconds(305)));
                services.AddSingleton<IVideoResolutionProbe>(new FixedResolutionProbe(1920, 1080));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_previewPath))
            {
                Directory.Delete(_previewPath, recursive: true);
            }
        }
    }

    private sealed class FixedDurationProbe(TimeSpan? duration) : IVideoDurationProbe
    {
        public Task<TimeSpan?> GetDurationAsync(string physicalPath, CancellationToken cancellationToken) =>
            Task.FromResult(duration);
    }

    private sealed class FixedResolutionProbe(int? width, int? height) : IVideoResolutionProbe
    {
        public Task<(int? Width, int? Height)> GetResolutionAsync(string physicalPath, CancellationToken cancellationToken) =>
            Task.FromResult((width, height));
    }

    private static TemporaryDirectory CreateArchive()
    {
        var root = new TemporaryDirectory();
        foreach (var folder in new[] { "Videos", "Pictures", "Music", "Documents", "Books", "Downloads", "Shared", "Family", "History", "Trash" })
        {
            Directory.CreateDirectory(Path.Combine(root.Path, folder));
        }

        Directory.CreateDirectory(Path.Combine(root.Path, "Videos", "Cuts"));
        Directory.CreateDirectory(Path.Combine(root.Path, "Videos", "VideoComposition"));
        return root;
    }

    private static string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"video-manager-archive-preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-archive-endpoints-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
