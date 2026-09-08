using Microsoft.Extensions.Options;
using WebApp.Client.Models;
using WebApp.Configuration;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class ArchiveServiceTests
{
    [Fact]
    public void List_maps_photos_to_pictures_and_orders_folders_first()
    {
        using var root = CreateArchive();
        Directory.CreateDirectory(Path.Combine(root.Path, "Pictures", "Zeta"));
        awaitFile(Path.Combine(root.Path, "Pictures", "alpha.txt"));
        Directory.CreateDirectory(Path.Combine(root.Path, "Pictures", "Alpha Folder"));

        var listing = CreateService(root.Path).List("photos", null);

        Assert.Equal("Photos", listing.Category.DisplayName);
        Assert.Equal(["Alpha Folder", "Zeta", "alpha.txt"], listing.Items.Select(item => item.Name));
        Assert.Equal(ArchiveItemKind.Folder, listing.Items[0].Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".hidden")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("bad/name")]
    [InlineData("bad\\name")]
    [InlineData("CON")]
    public void CreateFolder_rejects_unsafe_names(string name)
    {
        using var root = CreateArchive();
        var service = CreateService(root.Path);

        Assert.Throws<ArchiveValidationException>(() => service.CreateFolder("documents", null, name));
    }

    [Fact]
    public void CreateFolder_creates_inside_current_category()
    {
        using var root = CreateArchive();
        var service = CreateService(root.Path);

        var listing = service.CreateFolder("documents", null, "Invoices");

        Assert.Contains(listing.Items, item => item.Name == "Invoices" && item.Kind == ArchiveItemKind.Folder);
        Assert.True(Directory.Exists(Path.Combine(root.Path, "Documents", "Invoices")));
    }

    [Fact]
    public void Trash_does_not_allow_folder_creation()
    {
        using var root = CreateArchive();
        var service = CreateService(root.Path);

        Assert.Throws<ArchiveForbiddenException>(() => service.CreateFolder("trash", null, "Nope"));
    }

    [Fact]
    public void Rename_rejects_duplicate_sibling_name()
    {
        using var root = CreateArchive();
        Directory.CreateDirectory(Path.Combine(root.Path, "Documents", "A"));
        Directory.CreateDirectory(Path.Combine(root.Path, "Documents", "B"));
        var service = CreateService(root.Path);
        var item = service.List("documents", null).Items.Single(item => item.Name == "B");

        Assert.Throws<ArchiveConflictException>(() => service.Rename("documents", item.Id, "A"));
    }

    [Fact]
    public void MoveToTrash_moves_item_without_permanent_delete()
    {
        using var root = CreateArchive();
        var file = Path.Combine(root.Path, "Documents", "note.txt");
        awaitFile(file);
        var service = CreateService(root.Path);
        var item = service.List("documents", null).Items.Single();

        service.MoveToTrash("documents", item.Id);

        Assert.False(File.Exists(file));
        Assert.True(File.Exists(Path.Combine(root.Path, "Trash", "note.txt")));
    }

    [Fact]
    public void Video_files_are_marked_for_player_selection()
    {
        using var root = CreateArchive();
        awaitFile(Path.Combine(root.Path, "Videos", "clip.mp4"));
        var service = CreateService(root.Path);

        var item = Assert.Single(service.List("videos", null).Items);

        Assert.True(item.IsVideo);
        Assert.Equal(".mp4", item.Extension);
    }

    [Fact]
    public void Audio_files_are_marked_in_any_category()
    {
        using var root = CreateArchive();
        awaitFile(Path.Combine(root.Path, "Music", "song.mp3"));
        awaitFile(Path.Combine(root.Path, "Music", "beat.wav"));
        awaitFile(Path.Combine(root.Path, "Books", "voice.mp3"));
        var service = CreateService(root.Path);

        var music = service.List("music", null).Items;
        var book = Assert.Single(service.List("books", null).Items);

        Assert.All(music, item => Assert.True(item.IsMusic));
        Assert.All(music, item => Assert.False(item.IsVideo));
        Assert.True(book.IsMusic);
        Assert.False(book.IsVideo);
    }

    [Fact]
    public void Music_listing_uses_first_direct_image_as_album_cover()
    {
        using var root = CreateArchive();
        var album = Path.Combine(root.Path, "Music", "Album");
        Directory.CreateDirectory(album);
        Directory.CreateDirectory(Path.Combine(album, "Nested"));
        awaitFile(Path.Combine(album, "song.mp3"));
        awaitFile(Path.Combine(album, "zeta.png"));
        awaitFile(Path.Combine(album, "alpha.jpg"));
        awaitFile(Path.Combine(album, "Nested", "aardvark.jpg"));
        var service = CreateService(root.Path);
        var folder = service.List("music", null).Items.Single(item => item.Name == "Album");

        var listing = service.List("music", folder.Id);
        var track = listing.Items.Single(item => item.Name == "song.mp3");

        Assert.True(track.IsMusic);
        Assert.Equal(folder.Id, track.AlbumCoverId);
        Assert.True(service.TryResolveAlbumCover("music", folder.Id, out var cover));
        Assert.Equal("alpha.jpg", cover!.Name);
    }

    [Fact]
    public void Album_cover_resolver_works_for_any_category_folder()
    {
        using var root = CreateArchive();
        awaitFile(Path.Combine(root.Path, "Books", "cover.jpg"));
        var service = CreateService(root.Path);
        var folder = service.List("books", null).CurrentFolder;

        Assert.True(service.TryResolveAlbumCover("books", folder.Id, out var cover));
        Assert.Equal("cover.jpg", cover!.Name);
    }

    private static ArchiveService CreateService(string path) =>
        new(Options.Create(new ArchiveRootOptions { Path = path }));

    private static TemporaryDirectory CreateArchive()
    {
        var root = new TemporaryDirectory();
        foreach (var folder in new[] { "Videos", "Pictures", "Music", "Documents", "Books", "Downloads", "Shared", "Family", "History", "Trash" })
        {
            Directory.CreateDirectory(Path.Combine(root.Path, folder));
        }

        return root;
    }

    private static void awaitFile(string path) => File.WriteAllText(path, "content");

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-archive-{Guid.NewGuid():N}");
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
