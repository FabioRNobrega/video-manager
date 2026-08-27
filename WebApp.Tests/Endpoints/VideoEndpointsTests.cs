using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using WebApp.Client.Models;

namespace WebApp.Tests.Endpoints;

public sealed class VideoEndpointsTests
{
    [Fact]
    public async Task Scan_returns_only_the_browser_safe_contract()
    {
        using var root = new TemporaryDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(root.Path, "secret-directory-name"));
        await File.WriteAllBytesAsync(Path.Combine(nested.FullName, "clip.MP4"), [1, 2, 3, 4]);
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/api/videos/scan", content: null);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(root.Path, json);
        Assert.DoesNotContain("secret-directory-name", json);
        using var document = JsonDocument.Parse(json);
        var item = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(["extension", "id", "name", "sizeBytes"],
            item.EnumerateObject().Select(property => property.Name).OrderBy(name => name));
        Assert.Equal("clip.MP4", item.GetProperty("name").GetString());
        Assert.Equal(".mp4", item.GetProperty("extension").GetString());
        Assert.Equal(4, item.GetProperty("sizeBytes").GetInt64());
        Assert.Matches("^[0-9a-f]{32}$", item.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task Stream_supports_full_and_byte_range_requests()
    {
        using var root = new TemporaryDirectory();
        byte[] fixture = [10, 20, 30, 40, 50, 60, 70];
        await File.WriteAllBytesAsync(Path.Combine(root.Path, "clip.mp4"), fixture);
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();
        var video = Assert.Single((await (await client.PostAsync("/api/videos/scan", null))
            .Content.ReadFromJsonAsync<List<VideoItemDto>>())!);

        using var fullResponse = await client.GetAsync($"/api/videos/{video.Id}/stream");
        Assert.Equal(HttpStatusCode.OK, fullResponse.StatusCode);
        Assert.Equal("video/mp4", fullResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(fixture, await fullResponse.Content.ReadAsByteArrayAsync());

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/videos/{video.Id}/stream");
        request.Headers.Range = new RangeHeaderValue(2, 5);
        using var rangeResponse = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, rangeResponse.StatusCode);
        Assert.Contains("bytes", rangeResponse.Headers.AcceptRanges);
        Assert.Equal("bytes 2-5/7", rangeResponse.Content.Headers.ContentRange?.ToString());
        Assert.Equal(fixture[2..6], await rangeResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Unknown_malformed_stale_deleted_and_path_like_ids_return_not_found()
    {
        using var root = new TemporaryDirectory();
        var file = Path.Combine(root.Path, "clip.webm");
        await File.WriteAllBytesAsync(file, [1, 2, 3]);
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient();
        var first = await ScanSingleAsync(client);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/videos/not-an-id/stream")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/videos/{Guid.NewGuid():N}/stream")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/videos/%2Fetc%2Fpasswd/stream")).StatusCode);

        var second = await ScanSingleAsync(client);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/videos/{first.Id}/stream")).StatusCode);

        File.Delete(file);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/videos/{second.Id}/stream")).StatusCode);
    }

    [Fact]
    public async Task Scan_failure_is_path_free_and_invalidates_the_previous_snapshot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"video-manager-api-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(path, "clip.mov"), [1]);
            using var factory = new VideoManagerFactory(path);
            using var client = factory.CreateClient();
            var oldVideo = await ScanSingleAsync(client);
            Directory.Delete(path, recursive: true);

            using var failedScan = await client.PostAsync("/api/videos/scan", null);
            var problem = await failedScan.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.InternalServerError, failedScan.StatusCode);
            Assert.DoesNotContain(path, problem);
            Assert.DoesNotContain("clip.mov", problem);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync($"/api/videos/{oldVideo.Id}/stream")).StatusCode);
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Unapproved_host_header_is_rejected()
    {
        using var root = new TemporaryDirectory();
        using var factory = new VideoManagerFactory(root.Path);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/videos/scan");
        request.Headers.Host = "remote.example";

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<VideoItemDto> ScanSingleAsync(HttpClient client)
    {
        using var response = await client.PostAsync("/api/videos/scan", null);
        response.EnsureSuccessStatusCode();
        return Assert.Single((await response.Content.ReadFromJsonAsync<List<VideoItemDto>>())!);
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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-api-tests-{Guid.NewGuid():N}");
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
