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
    public void Image_files_are_marked_in_any_category()
    {
        using var root = CreateArchive();
        awaitFile(Path.Combine(root.Path, "Pictures", "photo.jpg"));
        awaitFile(Path.Combine(root.Path, "Pictures", "scan.jpeg"));
        awaitFile(Path.Combine(root.Path, "Pictures", "banner.png"));
        awaitFile(Path.Combine(root.Path, "Pictures", "notes.txt"));
        awaitFile(Path.Combine(root.Path, "Documents", "receipt.png"));
        var service = CreateService(root.Path);

        var pictures = service.List("photos", null).Items;
        var document = Assert.Single(service.List("documents", null).Items);

        Assert.Equal(3, pictures.Count(item => item.IsImage));
        Assert.True(pictures.Single(item => item.Name == "notes.txt") is { IsImage: false });
        Assert.True(document.IsImage);
        Assert.False(document.IsVideo);
        Assert.False(document.IsMusic);
    }

    [Fact]
    public void TryResolveImage_returns_false_for_folders_and_non_image_files()
    {
        using var root = CreateArchive();
        Directory.CreateDirectory(Path.Combine(root.Path, "Pictures", "Album"));
        awaitFile(Path.Combine(root.Path, "Pictures", "notes.txt"));
        var service = CreateService(root.Path);
        var folder = service.List("photos", null).Items.Single(item => item.Name == "Album");
        var textFile = service.List("photos", null).Items.Single(item => item.Name == "notes.txt");

        Assert.False(service.TryResolveImage("photos", folder.Id, out _));
        Assert.False(service.TryResolveImage("photos", textFile.Id, out _));
        Assert.False(service.TryResolveImage("photos", "unknown-id", out _));
    }

    [Fact]
    public void TryResolveImage_resolves_a_valid_image_item()
    {
        using var root = CreateArchive();
        awaitFile(Path.Combine(root.Path, "Pictures", "photo.jpg"));
        var service = CreateService(root.Path);
        var item = Assert.Single(service.List("photos", null).Items);

        Assert.True(service.TryResolveImage("photos", item.Id, out var resolved));
        Assert.Equal(Path.Combine(root.Path, "Pictures", "photo.jpg"), resolved!.PhysicalPath);
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

    [Fact]
    public void Epub_files_are_marked_as_books_only_inside_the_books_category()
    {
        using var root = CreateArchive();
        awaitFile(Path.Combine(root.Path, "Books", "novel.epub"));
        awaitFile(Path.Combine(root.Path, "Downloads", "archive.epub"));
        var service = CreateService(root.Path);

        var book = Assert.Single(service.List("books", null).Items);
        var download = Assert.Single(service.List("downloads", null).Items);

        Assert.True(book.IsBook);
        Assert.False(book.IsVideo);
        Assert.False(book.IsMusic);
        Assert.False(download.IsBook);
    }

    [Fact]
    public void Books_notes_helper_file_is_not_misclassified_as_book_content()
    {
        using var root = CreateArchive();
        Directory.CreateDirectory(Path.Combine(root.Path, "Books", "Notes"));
        awaitFile(Path.Combine(root.Path, "Books", "Notes", "pereneArchiveBookNotes.txt"));
        awaitFile(Path.Combine(root.Path, "Books", "novel.epub"));
        var service = CreateService(root.Path);

        var rootListing = service.List("books", null).Items;
        var notesFolder = rootListing.Single(item => item.Name == "Notes");
        var book = rootListing.Single(item => item.Name == "novel.epub");
        var notesFile = service.List("books", notesFolder.Id).Items.Single(item => item.Name == "pereneArchiveBookNotes.txt");

        Assert.False(notesFile.IsBook);
        Assert.True(book.IsBook);
    }

    [Fact]
    public void TextDocument_files_are_marked_in_any_category_and_nested_folders()
    {
        using var root = CreateArchive();
        awaitFile(Path.Combine(root.Path, "Documents", "notes.md"));
        awaitFile(Path.Combine(root.Path, "Documents", "readme.markdown"));
        awaitFile(Path.Combine(root.Path, "Documents", "plain.txt"));
        awaitFile(Path.Combine(root.Path, "Documents", "photo.jpg"));
        Directory.CreateDirectory(Path.Combine(root.Path, "Downloads", "Nested"));
        awaitFile(Path.Combine(root.Path, "Downloads", "Nested", "log.txt"));
        var service = CreateService(root.Path);

        var documents = service.List("documents", null).Items;
        var nestedFolder = service.List("downloads", null).Items.Single(item => item.Name == "Nested");
        var nestedFile = service.List("downloads", nestedFolder.Id).Items.Single(item => item.Name == "log.txt");

        Assert.Equal(3, documents.Count(item => item.IsTextDocument));
        Assert.True(documents.Single(item => item.Name == "notes.md").IsTextDocument);
        Assert.True(documents.Single(item => item.Name == "readme.markdown").IsTextDocument);
        Assert.True(documents.Single(item => item.Name == "plain.txt").IsTextDocument);
        Assert.False(documents.Single(item => item.Name == "photo.jpg").IsTextDocument);
        Assert.True(nestedFile.IsTextDocument);
    }

    [Fact]
    public void TryResolveTextDocument_returns_false_for_folders_and_unsupported_files()
    {
        using var root = CreateArchive();
        Directory.CreateDirectory(Path.Combine(root.Path, "Documents", "Folder"));
        awaitFile(Path.Combine(root.Path, "Documents", "photo.jpg"));
        var service = CreateService(root.Path);
        var folder = service.List("documents", null).Items.Single(item => item.Name == "Folder");
        var image = service.List("documents", null).Items.Single(item => item.Name == "photo.jpg");

        Assert.False(service.TryResolveTextDocument("documents", folder.Id, out _));
        Assert.False(service.TryResolveTextDocument("documents", image.Id, out _));
        Assert.False(service.TryResolveTextDocument("documents", "unknown-id", out _));
    }

    [Fact]
    public void TryResolveTextDocument_resolves_a_valid_markdown_item_in_any_category()
    {
        using var root = CreateArchive();
        awaitFile(Path.Combine(root.Path, "Books", "notes.md"));
        var service = CreateService(root.Path);
        var item = Assert.Single(service.List("books", null).Items);

        Assert.True(service.TryResolveTextDocument("books", item.Id, out var resolved));
        Assert.Equal(Path.Combine(root.Path, "Books", "notes.md"), resolved!.PhysicalPath);
    }

    [Fact]
    public void TryResolveBook_returns_false_for_folders_and_non_epub_files()
    {
        using var root = CreateArchive();
        Directory.CreateDirectory(Path.Combine(root.Path, "Books", "Series"));
        awaitFile(Path.Combine(root.Path, "Books", "notes.txt"));
        var service = CreateService(root.Path);
        var folder = service.List("books", null).Items.Single(item => item.Name == "Series");
        var textFile = service.List("books", null).Items.Single(item => item.Name == "notes.txt");

        Assert.False(service.TryResolveBook("books", folder.Id, out _));
        Assert.False(service.TryResolveBook("books", textFile.Id, out _));
        Assert.False(service.TryResolveBook("books", "unknown-id", out _));
    }

    [Fact]
    public void TryResolveBook_returns_false_when_extension_matches_outside_books_category()
    {
        using var root = CreateArchive();
        awaitFile(Path.Combine(root.Path, "Downloads", "archive.epub"));
        var service = CreateService(root.Path);
        var item = Assert.Single(service.List("downloads", null).Items);

        Assert.False(service.TryResolveBook("downloads", item.Id, out _));
    }

    [Fact]
    public void TryResolveBook_resolves_a_valid_epub_item()
    {
        using var root = CreateArchive();
        awaitFile(Path.Combine(root.Path, "Books", "novel.epub"));
        var service = CreateService(root.Path);
        var item = Assert.Single(service.List("books", null).Items);

        Assert.True(service.TryResolveBook("books", item.Id, out var resolved));
        Assert.Equal(Path.Combine(root.Path, "Books", "novel.epub"), resolved!.PhysicalPath);
    }

    [Fact]
    public void GetCategoryRootPath_returns_the_category_physical_folder()
    {
        using var root = CreateArchive();
        var service = CreateService(root.Path);

        Assert.Equal(Path.Combine(root.Path, "Pictures"), service.GetCategoryRootPath("photos"));
    }

    [Fact]
    public void ComputeItemId_matches_the_id_produced_by_listing()
    {
        using var root = CreateArchive();
        awaitFile(Path.Combine(root.Path, "Pictures", "photo.jpg"));
        var service = CreateService(root.Path);
        var item = Assert.Single(service.List("photos", null).Items);

        var computed = service.ComputeItemId("photos", Path.Combine(root.Path, "Pictures", "photo.jpg"));

        Assert.Equal(item.Id, computed);
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
