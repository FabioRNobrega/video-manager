using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class VideoLibraryServiceTests
{
    [Fact]
    public async Task Scan_discovers_supported_extensions_recursively_and_ignores_others()
    {
        using var root = new TemporaryDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(root.Path, "private-subfolder"));
        await File.WriteAllBytesAsync(Path.Combine(root.Path, "first.MP4"), [1, 2]);
        await File.WriteAllBytesAsync(Path.Combine(nested.FullName, "second.webm"), [3]);
        await File.WriteAllBytesAsync(Path.Combine(nested.FullName, "third.MOV"), [4, 5, 6]);
        await File.WriteAllBytesAsync(Path.Combine(root.Path, "fourth.m4v"), [7]);
        await File.WriteAllTextAsync(Path.Combine(root.Path, "ignore.txt"), "not a video");

        var entries = await CreateService(root.Path).ScanAsync();

        Assert.Equal(4, entries.Count);
        Assert.Equal([".m4v", ".mov", ".mp4", ".webm"],
            entries.Select(entry => entry.Extension).OrderBy(value => value));
        Assert.All(entries, entry =>
        {
            Assert.Matches("^[0-9a-f]{32}$", entry.Id);
            Assert.DoesNotContain("private-subfolder", entry.Name);
            Assert.Equal(Path.GetFileName(entry.PhysicalPath), entry.Name);
            Assert.True(entry.SizeBytes > 0);
            Assert.DoesNotContain('\\', entry.RelativePath);
            Assert.Equal(File.GetLastWriteTimeUtc(entry.PhysicalPath), entry.LastWriteTimeUtc);
        });
    }

    [Fact]
    public async Task Identity_metadata_reflects_normalized_relative_path()
    {
        using var root = new TemporaryDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(root.Path, "nested"));
        await File.WriteAllBytesAsync(Path.Combine(nested.FullName, "clip.mp4"), [1, 2, 3]);

        var entry = Assert.Single(await CreateService(root.Path).ScanAsync());

        Assert.Equal("nested/clip.mp4", entry.RelativePath);
    }

    [Fact]
    public async Task Rescan_replaces_all_opaque_ids_and_snapshot_authorization()
    {
        using var root = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(root.Path, "clip.mp4"), [1]);
        var service = CreateService(root.Path);

        var first = Assert.Single(await service.ScanAsync());
        var second = Assert.Single(await service.ScanAsync());

        Assert.NotEqual(first.Id, second.Id);
        Assert.False(service.TryResolve(first.Id, out _));
        Assert.True(service.TryResolve(second.Id, out var resolved));
        Assert.Equal(second, resolved);
    }

    [Fact]
    public async Task Empty_directory_produces_empty_snapshot()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root.Path);

        Assert.Empty(await service.ScanAsync());
        Assert.False(service.TryResolve(Guid.NewGuid().ToString("N"), out _));
    }

    [Fact]
    public async Task Failed_scan_clears_the_previous_snapshot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"video-manager-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(path, "clip.mp4"), [1]);
            var service = CreateService(path);
            var oldEntry = Assert.Single(await service.ScanAsync());
            Directory.Delete(path, recursive: true);

            await Assert.ThrowsAnyAsync<IOException>(() => service.ScanAsync());
            Assert.False(service.TryResolve(oldEntry.Id, out _));
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
    public async Task Precancelled_scan_clears_the_previous_snapshot()
    {
        using var root = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(root.Path, "clip.mp4"), [1]);
        var service = CreateService(root.Path);
        var oldEntry = Assert.Single(await service.ScanAsync());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ScanAsync(cancellation.Token));
        Assert.False(service.TryResolve(oldEntry.Id, out _));
    }

    [Fact]
    public async Task File_and_directory_symlinks_are_skipped()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var root = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        var outsideFile = Path.Combine(outside.Path, "outside.mp4");
        await File.WriteAllBytesAsync(outsideFile, [1, 2, 3]);
        File.CreateSymbolicLink(Path.Combine(root.Path, "linked-file.mp4"), outsideFile);
        Directory.CreateSymbolicLink(Path.Combine(root.Path, "linked-directory"), outside.Path);
        await File.WriteAllBytesAsync(Path.Combine(root.Path, "inside.mp4"), [4]);

        var entries = await CreateService(root.Path).ScanAsync();

        Assert.Equal("inside.mp4", Assert.Single(entries).Name);
    }

    [Fact]
    public void Containment_is_path_separator_aware_for_sibling_prefixes()
    {
        using var parent = new TemporaryDirectory();
        var root = Directory.CreateDirectory(Path.Combine(parent.Path, "root")).FullName;
        var sibling = Directory.CreateDirectory(Path.Combine(parent.Path, "root-other")).FullName;

        Assert.True(VideoLibraryService.IsWithinRoot(root, Path.Combine(root, "clip.mp4")));
        Assert.False(VideoLibraryService.IsWithinRoot(root, Path.Combine(sibling, "clip.mp4")));
    }

    [Fact]
    public async Task Resolve_remains_safe_during_concurrent_rescans()
    {
        using var root = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(root.Path, "clip.mp4"), [1]);
        var service = CreateService(root.Path);
        var first = Assert.Single(await service.ScanAsync());

        var scans = Enumerable.Range(0, 8).Select(_ => service.ScanAsync()).ToArray();
        for (var index = 0; index < 100; index++)
        {
            _ = service.TryResolve(first.Id, out _);
        }

        await Task.WhenAll(scans);
        Assert.False(service.TryResolve(first.Id, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/videos")]
    public void Configuration_rejects_missing_or_relative_paths(string path)
    {
        var options = new VideoLibraryOptions { Path = path };

        Assert.False(VideoLibraryOptions.HasConfiguredPath(options) && VideoLibraryOptions.HasAbsolutePath(options));
    }

    [Fact]
    public async Task GetCurrentSnapshot_returns_scanned_entries_without_rescanning()
    {
        using var root = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(root.Path, "clip.mp4"), [1]);
        var service = CreateService(root.Path);

        Assert.Empty(service.GetCurrentSnapshot());

        var scanned = await service.ScanAsync();
        File.Delete(Path.Combine(root.Path, "clip.mp4"));

        Assert.Equal(scanned, service.GetCurrentSnapshot());
    }

    private static readonly string SharedPreviewPath = CreateSharedPreviewDirectory();

    private static string CreateSharedPreviewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"video-manager-tests-preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static VideoLibraryService CreateService(string path) =>
        new(Options.Create(new VideoLibraryOptions { Path = path }), CreateThumbnailCoordinator());

    private static ThumbnailCoordinator CreateThumbnailCoordinator()
    {
        var options = Options.Create(new ThumbnailCacheOptions { Path = SharedPreviewPath, QueueCapacity = 64 });
        return new ThumbnailCoordinator(new ThumbnailCache(options), new ThumbnailJobQueue(options));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-tests-{Guid.NewGuid():N}");
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
