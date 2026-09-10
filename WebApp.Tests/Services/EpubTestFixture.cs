using System.IO.Compression;
using System.Text;

namespace WebApp.Tests.Services;

/// <summary>
/// Builds a small, valid, non-DRM EPUB 3 file at test time so tests never depend on a committed/licensed book file.
/// </summary>
internal static class EpubTestFixture
{
    // A 1x1 transparent PNG, used as a tiny embedded cover image.
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    public sealed record SpineEntry(string FileName, string Heading, string Paragraph, string? NavTitle = null);

    public static string CreateMinimalEpub(
        string filePath,
        string title = "Test Book",
        string author = "Test Author",
        bool includeCover = true,
        bool includeNavigation = true)
    {
        return CreateEpub(
            filePath,
            [
                new SpineEntry("chapter1.xhtml", "Chapter One", "This is the first chapter of the test book.", "Chapter One"),
                new SpineEntry("chapter2.xhtml", "Chapter Two", "This is the second chapter of the test book.", "Chapter Two")
            ],
            title,
            author,
            includeCover,
            includeNavigation);
    }

    public static string CreateEpub(
        string filePath,
        IReadOnlyList<SpineEntry> spineEntries,
        string title = "Test Book",
        string author = "Test Author",
        bool includeCover = true,
        bool includeNavigation = true)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);

        var mimetypeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var writer = new StreamWriter(mimetypeEntry.Open(), new UTF8Encoding(false)))
        {
            writer.Write("application/epub+zip");
        }

        WriteEntry(archive, "META-INF/container.xml", ContainerXml);
        WriteEntry(archive, "OEBPS/content.opf", BuildContentOpf(title, author, includeCover, spineEntries));
        if (includeNavigation)
        {
            WriteEntry(archive, "OEBPS/nav.xhtml", BuildNavXhtml(spineEntries));
        }

        foreach (var entry in spineEntries)
        {
            WriteEntry(archive, $"OEBPS/{entry.FileName}", BuildChapterXhtml(entry.Heading, entry.Paragraph));
        }

        if (includeCover)
        {
            WriteBinaryEntry(archive, "OEBPS/cover.png", Convert.FromBase64String(OnePixelPngBase64));
        }

        return filePath;
    }

    public static string CreateMalformedEpub(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        // Not a valid zip/EPUB archive at all.
        File.WriteAllText(filePath, "this is not a valid epub file");
        return filePath;
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void WriteBinaryEntry(ZipArchive archive, string entryName, byte[] content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private const string ContainerXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
          <rootfiles>
            <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
          </rootfiles>
        </container>
        """;

    private static string BuildNavXhtml(IReadOnlyList<SpineEntry> spineEntries)
    {
        var listItems = string.Join(
            Environment.NewLine,
            spineEntries
                .Where(entry => entry.NavTitle is not null)
                .Select(entry => $"""    <li><a href="{entry.FileName}">{entry.NavTitle}</a></li>"""));

        return $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
        <head><title>Table of Contents</title></head>
        <body>
        <nav epub:type="toc" id="toc">
          <ol>
        {listItems}
          </ol>
        </nav>
        </body>
        </html>
        """;
    }

    private static string BuildContentOpf(string title, string author, bool includeCover, IReadOnlyList<SpineEntry> spineEntries)
    {
        var coverManifestItem = includeCover
            ? """<item id="cover-image" href="cover.png" media-type="image/png" properties="cover-image"/>"""
            : string.Empty;

        var manifestItems = string.Join(
            Environment.NewLine,
            spineEntries.Select((entry, index) =>
                $"""    <item id="chapter{index}" href="{entry.FileName}" media-type="application/xhtml+xml"/>"""));
        var spineItems = string.Join(
            Environment.NewLine,
            spineEntries.Select((_, index) => $"""    <itemref idref="chapter{index}"/>"""));

        return $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="bookid">
          <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
            <dc:identifier id="bookid">urn:uuid:12345678-1234-1234-1234-123456789012</dc:identifier>
            <dc:title>{title}</dc:title>
            <dc:creator>{author}</dc:creator>
            <dc:language>en</dc:language>
            <meta property="dcterms:modified">2024-01-01T00:00:00Z</meta>
          </metadata>
          <manifest>
            <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
        {manifestItems}
            {coverManifestItem}
          </manifest>
          <spine>
        {spineItems}
          </spine>
        </package>
        """;
    }

    private static string BuildChapterXhtml(string heading, string paragraph) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml">
        <head><title>{heading}</title></head>
        <body><h1>{heading}</h1><p>{paragraph}</p><script>alert('unsafe')</script></body>
        </html>
        """;
}
