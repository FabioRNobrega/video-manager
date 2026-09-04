using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class VideoCompositionServiceTests
{
    [Fact]
    public async Task Scan_discovers_supported_top_level_compositions_and_ignores_others()
    {
        using var compositions = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(compositions.Path, "Jennifer White Composition 0001.MP4"), [1, 2]);
        await File.WriteAllBytesAsync(Path.Combine(compositions.Path, "ignore.txt"), [1]);
        Directory.CreateDirectory(Path.Combine(compositions.Path, "nested"));
        await File.WriteAllBytesAsync(Path.Combine(compositions.Path, "nested", "Nested Composition 0001.mp4"), [1]);

        var entries = await CreateService(compositions.Path).ScanAsync();

        var entry = Assert.Single(entries);
        Assert.Equal("Jennifer White Composition 0001.MP4", entry.Name);
        Assert.Equal(".mp4", entry.Extension);
        Assert.Matches("^[0-9a-f]{32}$", entry.Id);
    }

    [Fact]
    public async Task Rescan_replaces_opaque_ids()
    {
        using var compositions = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(compositions.Path, "Composition 0001.mp4"), [1]);
        var service = CreateService(compositions.Path);

        var first = Assert.Single(await service.ScanAsync());
        var second = Assert.Single(await service.ScanAsync());

        Assert.NotEqual(first.Id, second.Id);
        Assert.False(service.TryResolve(first.Id, out _));
        Assert.True(service.TryResolve(second.Id, out var resolved));
        Assert.Equal(second, resolved);
    }

    [Fact]
    public async Task File_and_directory_symlinks_are_skipped()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var compositions = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        var outsideFile = Path.Combine(outside.Path, "outside.mp4");
        await File.WriteAllBytesAsync(outsideFile, [1, 2, 3]);
        File.CreateSymbolicLink(Path.Combine(compositions.Path, "linked-file.mp4"), outsideFile);
        Directory.CreateSymbolicLink(Path.Combine(compositions.Path, "linked-directory"), outside.Path);
        await File.WriteAllBytesAsync(Path.Combine(compositions.Path, "inside.mp4"), [4]);

        var entries = await CreateService(compositions.Path).ScanAsync();

        Assert.Equal("inside.mp4", Assert.Single(entries).Name);
    }

    private static VideoCompositionService CreateService(string path) =>
        new(Options.Create(new VideoCompositionOptions { Path = path }));

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-composition-service-{Guid.NewGuid():N}");
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
