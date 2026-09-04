using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace WebApp.Tests.Client;

public sealed class ComingSoonSectionTests
{
    [Theory]
    [InlineData("/photos", "bi-image", "Photos are coming soon")]
    [InlineData("/music", "bi-music-note-beamed", "Music is coming soon")]
    [InlineData("/documents", "bi-file-earmark-text", "Documents are coming soon")]
    [InlineData("/downloads", "bi-download", "Downloads are coming soon")]
    [InlineData("/shared", "bi-people", "Shared access is coming soon")]
    [InlineData("/family", "bi-house-heart", "Family is coming soon")]
    [InlineData("/history", "bi-clock-history", "History is coming soon")]
    [InlineData("/trash", "bi-trash3", "Trash is coming soon")]
    public async Task Placeholder_route_renders_the_given_icon_title_and_message_without_fetching_data(
        string route, string icon, string title)
    {
        using var root = new TemporaryDirectory();
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync(route);

        Assert.Contains($"bi {icon}", html);
        Assert.Contains(title, html);
        Assert.DoesNotContain("api/videos", html);
        Assert.DoesNotContain("api/cuts", html);
        Assert.DoesNotContain("api/compositions", html);
    }

    private sealed class VideoManagerFactory : WebApplicationFactory<Program>
    {
        private readonly string _rootPath;
        private readonly string _previewPath;
        private readonly string _cutPath;
        private readonly string _compositionPath;

        public VideoManagerFactory(string rootPath)
        {
            _rootPath = rootPath;
            _previewPath = Path.Combine(Path.GetTempPath(), $"video-manager-coming-soon-preview-{Guid.NewGuid():N}");
            _cutPath = Path.Combine(Path.GetTempPath(), $"video-manager-coming-soon-cuts-{Guid.NewGuid():N}");
            _compositionPath = Path.Combine(Path.GetTempPath(), $"video-manager-coming-soon-composition-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_previewPath);
            Directory.CreateDirectory(_cutPath);
            Directory.CreateDirectory(_compositionPath);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["VideoLibrary:Path"] = _rootPath,
                    ["ThumbnailCache:Path"] = _previewPath,
                    ["VideoCut:Path"] = _cutPath,
                    ["VideoComposition:Path"] = _compositionPath,
                }));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_previewPath))
            {
                Directory.Delete(_previewPath, recursive: true);
            }
            if (disposing && Directory.Exists(_cutPath))
            {
                Directory.Delete(_cutPath, recursive: true);
            }
            if (disposing && Directory.Exists(_compositionPath))
            {
                Directory.Delete(_compositionPath, recursive: true);
            }
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-coming-soon-{Guid.NewGuid():N}");
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
