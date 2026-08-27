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
    }

    [Fact]
    public async Task Theme_bootstrap_executes_before_application_styles()
    {
        using var root = new TemporaryDirectory();
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");
        var colorSchemeIndex = html.IndexOf("name=\"color-scheme\"", StringComparison.Ordinal);
        var themeScriptIndex = html.IndexOf("js/theme", StringComparison.Ordinal);
        var bootstrapStylesIndex = html.IndexOf("lib/bootstrap/dist/css/bootstrap", StringComparison.Ordinal);
        var applicationStylesIndex = html.IndexOf("app.", StringComparison.Ordinal);

        Assert.True(colorSchemeIndex >= 0);
        Assert.True(themeScriptIndex > colorSchemeIndex);
        Assert.True(bootstrapStylesIndex > themeScriptIndex);
        Assert.True(applicationStylesIndex > themeScriptIndex);
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

    private sealed class VideoManagerFactory(string rootPath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["VideoLibrary:Path"] = rootPath }));
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
