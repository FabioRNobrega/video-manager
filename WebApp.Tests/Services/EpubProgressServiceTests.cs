using Microsoft.Extensions.Options;
using WebApp.Client.Models;
using WebApp.Configuration;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class EpubProgressServiceTests
{
    private static readonly DateTime LastWriteTimeUtc = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LoadProgressAsync_returns_null_when_nothing_has_been_saved()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root.Path);

        var progress = await service.LoadProgressAsync("books", "item-1", 1024, LastWriteTimeUtc, CancellationToken.None);

        Assert.Null(progress);
    }

    [Fact]
    public async Task SaveProgressAsync_then_LoadProgressAsync_round_trips_the_same_values()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root.Path);
        var saved = new BookProgressDto("3", 420);

        await service.SaveProgressAsync("books", "item-1", 1024, LastWriteTimeUtc, saved, CancellationToken.None);
        var loaded = await service.LoadProgressAsync("books", "item-1", 1024, LastWriteTimeUtc, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("3", loaded!.ChapterId);
        Assert.Equal(420, loaded.WordOffset);
    }

    [Fact]
    public async Task SaveProgressAsync_overwrites_previous_progress_for_the_same_book()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root.Path);

        await service.SaveProgressAsync("books", "item-1", 1024, LastWriteTimeUtc, new BookProgressDto("1", 100), CancellationToken.None);
        await service.SaveProgressAsync("books", "item-1", 1024, LastWriteTimeUtc, new BookProgressDto("5", 900), CancellationToken.None);

        var loaded = await service.LoadProgressAsync("books", "item-1", 1024, LastWriteTimeUtc, CancellationToken.None);

        Assert.Equal("5", loaded!.ChapterId);
        Assert.Equal(900, loaded.WordOffset);
    }

    [Fact]
    public async Task Different_items_do_not_collide_even_with_the_same_id_under_a_different_identity()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root.Path);

        await service.SaveProgressAsync("books", "item-1", 1024, LastWriteTimeUtc, new BookProgressDto("1", 100), CancellationToken.None);
        await service.SaveProgressAsync("books", "item-1", 2048, LastWriteTimeUtc, new BookProgressDto("9", 900), CancellationToken.None);

        var originalVersionProgress = await service.LoadProgressAsync("books", "item-1", 1024, LastWriteTimeUtc, CancellationToken.None);
        var replacedVersionProgress = await service.LoadProgressAsync("books", "item-1", 2048, LastWriteTimeUtc, CancellationToken.None);

        Assert.Equal("1", originalVersionProgress!.ChapterId);
        Assert.Equal("9", replacedVersionProgress!.ChapterId);
    }

    [Fact]
    public async Task LoadProgressAsync_falls_back_gracefully_when_the_progress_file_is_malformed()
    {
        using var root = new TemporaryDirectory();
        var notesFolder = Path.Combine(root.Path, "Books", "Notes");
        Directory.CreateDirectory(notesFolder);
        await File.WriteAllTextAsync(Path.Combine(notesFolder, "pereneArchiveBookProgress.json"), "{ this is not valid json");
        var service = CreateService(root.Path);

        var progress = await service.LoadProgressAsync("books", "item-1", 1024, LastWriteTimeUtc, CancellationToken.None);

        Assert.Null(progress);
    }

    [Fact]
    public async Task SaveProgressAsync_does_not_leave_temp_files_behind()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root.Path);

        await service.SaveProgressAsync("books", "item-1", 1024, LastWriteTimeUtc, new BookProgressDto("1", 500), CancellationToken.None);

        var notesFolder = Path.Combine(root.Path, "Books", "Notes");
        var leftoverTempFiles = Directory.GetFiles(notesFolder, "*.tmp");
        Assert.Empty(leftoverTempFiles);
        Assert.True(File.Exists(Path.Combine(notesFolder, "pereneArchiveBookProgress.json")));
    }

    private static EpubProgressService CreateService(string archiveRootPath) =>
        new(Options.Create(new ArchiveRootOptions { Path = archiveRootPath }));

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-epub-progress-service-{Guid.NewGuid():N}");
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
