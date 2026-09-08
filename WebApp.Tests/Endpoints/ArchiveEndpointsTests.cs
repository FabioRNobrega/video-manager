using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using WebApp.Client.Models;

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

    private sealed class VideoManagerFactory(string archiveRoot) : WebApplicationFactory<Program>
    {
        private readonly string _previewPath = CreateDirectory();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ArchiveRoot:Path"] = archiveRoot,
                    ["VideoLibrary:Path"] = Path.Combine(archiveRoot, "Videos"),
                    ["ThumbnailCache:Path"] = _previewPath,
                    ["VideoCut:Path"] = Path.Combine(archiveRoot, "Videos", "Cuts"),
                    ["VideoComposition:Path"] = Path.Combine(archiveRoot, "Videos", "VideoComposition"),
                    ["HoverPreview:Enabled"] = "false"
                }));
        }
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
