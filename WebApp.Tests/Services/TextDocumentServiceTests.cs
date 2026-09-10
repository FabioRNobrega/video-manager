using System.Text;
using WebApp.Client.Models;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class TextDocumentServiceTests
{
    [Fact]
    public void Load_renders_common_markdown_extensions()
    {
        using var root = new TemporaryDirectory();
        var path = Path.Combine(root.Path, "notes.md");
        File.WriteAllText(path, "# Title\n\n~~gone~~\n\n- [x] done\n- [ ] todo\n\n| A | B |\n| - | - |\n| 1 | 2 |\n");
        var item = CreateEntry(path);
        var service = new TextDocumentService();

        var result = service.Load(item);

        Assert.Equal(TextDocumentKind.Markdown, result.DocumentKind);
        Assert.Contains("<h1", result.PreviewHtml);
        Assert.Contains("<del>", result.PreviewHtml);
        Assert.Contains("<table>", result.PreviewHtml);
        Assert.Contains("type=\"checkbox\"", result.PreviewHtml);
        Assert.Contains("disabled", result.PreviewHtml);
    }

    [Fact]
    public void Load_disables_raw_html_and_removes_images()
    {
        using var root = new TemporaryDirectory();
        var path = Path.Combine(root.Path, "notes.md");
        File.WriteAllText(path, "<script>alert(1)</script>\n\n![alt](https://example.com/pic.png)\n\nHello");
        var item = CreateEntry(path);
        var service = new TextDocumentService();

        var result = service.Load(item);

        Assert.DoesNotContain("<script", result.PreviewHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", result.PreviewHtml);
        Assert.DoesNotContain("<img", result.PreviewHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hello", result.PreviewHtml);
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com", false)]
    [InlineData("//example.com", false)]
    [InlineData("/relative/path", false)]
    [InlineData("mailto:someone@example.com", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,hi", false)]
    public void RenderPreview_only_allows_https_links(string href, bool shouldBeLive)
    {
        var item = CreateEntry(Path.Combine(Path.GetTempPath(), "link.md"));
        var service = new TextDocumentService();
        var source = $"[click]({href})";

        var html = service.RenderPreview(item, source);

        if (shouldBeLive)
        {
            Assert.Contains("href=\"https://example.com/\"", html);
            Assert.Contains("target=\"_blank\"", html);
            Assert.Contains("rel=\"noopener noreferrer nofollow\"", html);
        }
        else
        {
            Assert.DoesNotContain("href=", html);
        }

        Assert.Contains("click", html);
    }

    [Fact]
    public void Load_plaintext_preserves_whitespace()
    {
        using var root = new TemporaryDirectory();
        var path = Path.Combine(root.Path, "notes.txt");
        File.WriteAllText(path, "line one\n\n  indented   spaces\nline three");
        var item = CreateEntry(path);
        var service = new TextDocumentService();

        var result = service.Load(item);

        Assert.Equal(TextDocumentKind.PlainText, result.DocumentKind);
        Assert.Contains("<pre", result.PreviewHtml);
        Assert.Contains("line one\n\n  indented   spaces\nline three", result.PreviewHtml);
    }

    [Fact]
    public void Load_rejects_oversized_document()
    {
        using var root = new TemporaryDirectory();
        var path = Path.Combine(root.Path, "big.txt");
        File.WriteAllText(path, new string('a', TextDocumentService.MaxSourceBytes + 1));
        var item = CreateEntry(path);
        var service = new TextDocumentService();

        Assert.Throws<TextDocumentValidationException>(() => service.Load(item));
    }

    [Fact]
    public void Load_rejects_invalid_utf8_content()
    {
        using var root = new TemporaryDirectory();
        var path = Path.Combine(root.Path, "bad.txt");
        File.WriteAllBytes(path, [0xFF, 0xFE, 0x00, 0x01]);
        var item = CreateEntry(path);
        var service = new TextDocumentService();

        Assert.Throws<TextDocumentValidationException>(() => service.Load(item));
    }

    [Fact]
    public void RenderPreview_rejects_oversized_submitted_source()
    {
        var item = CreateEntry(Path.Combine(Path.GetTempPath(), "notes.md"));
        var service = new TextDocumentService();

        Assert.Throws<TextDocumentValidationException>(
            () => service.RenderPreview(item, new string('a', TextDocumentService.MaxSourceBytes + 1)));
    }

    [Fact]
    public void Save_writes_the_file_atomically_and_returns_a_new_revision()
    {
        using var root = new TemporaryDirectory();
        var path = Path.Combine(root.Path, "notes.txt");
        File.WriteAllText(path, "original");
        var item = CreateEntry(path);
        var service = new TextDocumentService();
        var loaded = service.Load(item);

        var result = service.Save(item, "updated content", loaded.Revision);

        Assert.Equal("updated content", File.ReadAllText(path));
        Assert.NotEqual(loaded.Revision, result.Revision);
        Assert.DoesNotContain(Directory.EnumerateFiles(root.Path), file => file.Contains(".tmp-"));
    }

    [Fact]
    public void Save_rejects_a_stale_revision_and_does_not_change_the_file()
    {
        using var root = new TemporaryDirectory();
        var path = Path.Combine(root.Path, "notes.txt");
        File.WriteAllText(path, "original");
        var item = CreateEntry(path);
        var service = new TextDocumentService();
        var loaded = service.Load(item);

        Thread.Sleep(10);
        File.WriteAllText(path, "changed by someone else");

        Assert.Throws<TextDocumentConflictException>(() => service.Save(item, "my draft", loaded.Revision));
        Assert.Equal("changed by someone else", File.ReadAllText(path));
    }

    [Fact]
    public void Save_rejects_oversized_source()
    {
        using var root = new TemporaryDirectory();
        var path = Path.Combine(root.Path, "notes.txt");
        File.WriteAllText(path, "original");
        var item = CreateEntry(path);
        var service = new TextDocumentService();
        var loaded = service.Load(item);

        Assert.Throws<TextDocumentValidationException>(
            () => service.Save(item, new string('a', TextDocumentService.MaxSourceBytes + 1), loaded.Revision));
    }

    private static ArchiveItemEntry CreateEntry(string path)
    {
        ArchiveCategory.TryGet("documents", out var category);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var exists = File.Exists(path);
        var info = exists ? new FileInfo(path) : null;
        return new ArchiveItemEntry(
            "test-id",
            category!,
            path,
            Path.GetFileName(path),
            ArchiveItemKind.File,
            extension,
            info?.Length,
            info?.LastWriteTimeUtc ?? DateTime.UtcNow,
            IsVideo: false,
            IsTextDocument: true);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-textdoc-{Guid.NewGuid():N}");
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
