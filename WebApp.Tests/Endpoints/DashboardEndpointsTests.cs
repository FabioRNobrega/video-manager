using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace WebApp.Tests.Endpoints;

public sealed class DashboardEndpointsTests
{
    [Theory]
    [InlineData("/api/dashboard/system")]
    [InlineData("/api/dashboard/memory")]
    [InlineData("/api/dashboard/storage")]
    [InlineData("/api/dashboard/network")]
    [InlineData("/api/dashboard/archive")]
    [InlineData("/api/dashboard/docker")]
    [InlineData("/api/dashboard/health")]
    [InlineData("/api/dashboard/history")]
    [InlineData("/api/dashboard/alerts")]
    public async Task Dashboard_endpoint_returns_ok_and_never_500(string path)
    {
        using var root = new TemporaryDirectory();
        using var factory = new DashboardFactory(root.Path);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class DashboardFactory : WebApplicationFactory<Program>
    {
        private readonly string _rootPath;
        private readonly string _previewPath;
        private readonly string _cutPath;
        private readonly string _compositionPath;

        public DashboardFactory(string rootPath)
        {
            _rootPath = rootPath;
            _previewPath = Path.Combine(Path.GetTempPath(), $"dashboard-api-preview-{Guid.NewGuid():N}");
            _cutPath = Path.Combine(Path.GetTempPath(), $"dashboard-api-cuts-{Guid.NewGuid():N}");
            _compositionPath = Path.Combine(Path.GetTempPath(), $"dashboard-api-composition-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_previewPath);
            Directory.CreateDirectory(_cutPath);
            Directory.CreateDirectory(_compositionPath);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ArchiveRoot:Path"] = _rootPath,
                    ["VideoLibrary:Path"] = _rootPath,
                    ["ThumbnailCache:Path"] = _previewPath,
                    ["VideoCut:Path"] = _cutPath,
                    ["VideoComposition:Path"] = _compositionPath,
                }));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
            {
                return;
            }

            foreach (var path in new[] { _previewPath, _cutPath, _compositionPath })
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dashboard-api-root-{Guid.NewGuid():N}");
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
