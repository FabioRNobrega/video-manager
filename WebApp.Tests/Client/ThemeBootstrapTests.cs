using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace WebApp.Tests.Client;

public sealed class ThemeBootstrapTests
{
    [Fact]
    public async Task Root_document_declares_dark_as_the_server_fallback()
    {
        using var root = new TemporaryDirectory();
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains("<html lang=\"en\" data-bs-theme=\"dark\">", html);
        Assert.Contains("<meta name=\"color-scheme\" content=\"dark light\"", html);
        Assert.Contains("Perene Tech Videos", html);
    }

    [Fact]
    public async Task Root_document_loads_design_assets_in_required_order()
    {
        using var root = new TemporaryDirectory();
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");
        var colorSchemeIndex = html.IndexOf("name=\"color-scheme\"", StringComparison.Ordinal);
        var themeScriptIndex = html.IndexOf("js/theme", StringComparison.Ordinal);
        var fontStylesIndex = html.IndexOf("fonts.googleapis.com/css2?family=Montserrat", StringComparison.Ordinal);
        var bootstrapStylesIndex = html.IndexOf("bootstrap@5.3.8/dist/css/bootstrap.min.css", StringComparison.Ordinal);
        var iconStylesIndex = html.IndexOf("bootstrap-icons@1.13.1/font/bootstrap-icons.min.css", StringComparison.Ordinal);
        var applicationStylesIndex = html.IndexOf("app.", StringComparison.Ordinal);
        var isolatedStylesIndex = html.IndexOf("WebApp.", applicationStylesIndex + 1, StringComparison.Ordinal);
        var blazorScriptIndex = html.IndexOf("_framework/blazor.web.js", StringComparison.Ordinal);
        var bootstrapBundleIndex = html.IndexOf("bootstrap@5.3.8/dist/js/bootstrap.bundle.min.js", StringComparison.Ordinal);

        Assert.True(colorSchemeIndex >= 0);
        Assert.True(themeScriptIndex > colorSchemeIndex);
        Assert.True(fontStylesIndex > themeScriptIndex);
        Assert.True(bootstrapStylesIndex > fontStylesIndex);
        Assert.True(iconStylesIndex > bootstrapStylesIndex);
        Assert.True(applicationStylesIndex > iconStylesIndex);
        Assert.True(isolatedStylesIndex > applicationStylesIndex);
        Assert.True(blazorScriptIndex > isolatedStylesIndex);
        Assert.True(bootstrapBundleIndex > blazorScriptIndex);

        Assert.Contains("family=Zilla+Slab", html);
        Assert.Contains("integrity=\"sha384-sRIl4kxILFvY47J16cr9ZwB07vP4J8+LH7qKQnuqkuIAvNWLzeN8tE5YBujZqJLB\"", html);
        Assert.Contains("integrity=\"sha384-FKyoEForCGlyvwx9Hj09JcYn3nv7wiPVlz7YYwJrWVcXK/BmnVDxM+D2scQbITxI\"", html);
        Assert.DoesNotContain("lib/bootstrap/dist/css/bootstrap.min.css", html);
    }

    [Fact]
    public async Task Theme_has_no_server_preference_or_endpoint()
    {
        using var root = new TemporaryDirectory();
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var pageResponse = await client.GetAsync("/");
        var html = await pageResponse.Content.ReadAsStringAsync();
        using var endpointResponse = await client.GetAsync("/api/theme");

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        if (pageResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            Assert.DoesNotContain(cookies, cookie =>
                cookie.Contains("theme", StringComparison.OrdinalIgnoreCase));
        }
        Assert.DoesNotContain("video-manager-theme", html);
        Assert.Equal(HttpStatusCode.NotFound, endpointResponse.StatusCode);
    }

    private sealed class VideoManagerFactory : WebApplicationFactory<Program>
    {
        private readonly string _rootPath;
        private readonly string _previewPath;
        private readonly string _cutPath;

        public VideoManagerFactory(string rootPath)
        {
            _rootPath = rootPath;
            _previewPath = Path.Combine(Path.GetTempPath(), $"video-manager-theme-tests-preview-{Guid.NewGuid():N}");
            _cutPath = Path.Combine(Path.GetTempPath(), $"video-manager-theme-tests-cuts-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_previewPath);
            Directory.CreateDirectory(_cutPath);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["VideoLibrary:Path"] = _rootPath,
                    ["ThumbnailCache:Path"] = _previewPath,
                    ["VideoCut:Path"] = _cutPath,
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
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"video-manager-theme-tests-{Guid.NewGuid():N}");
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
