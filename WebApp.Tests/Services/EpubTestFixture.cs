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

    public static string CreateMinimalEpub(
        string filePath,
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
        WriteEntry(archive, "OEBPS/content.opf", BuildContentOpf(title, author, includeCover));
        if (includeNavigation)
        {
            WriteEntry(archive, "OEBPS/nav.xhtml", NavXhtml);
        }

        WriteEntry(archive, "OEBPS/chapter1.xhtml", BuildChapterXhtml("Chapter One", "This is the first chapter of the test book."));
        WriteEntry(archive, "OEBPS/chapter2.xhtml", BuildChapterXhtml("Chapter Two", "This is the second chapter of the test book."));

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

    private const string NavXhtml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
        <head><title>Table of Contents</title></head>
        <body>
        <nav epub:type="toc" id="toc">
          <ol>
            <li><a href="chapter1.xhtml">Chapter One</a></li>
            <li><a href="chapter2.xhtml">Chapter Two</a></li>
          </ol>
        </nav>
        </body>
        </html>
        """;

    private static string BuildContentOpf(string title, string author, bool includeCover)
    {
        var coverManifestItem = includeCover
            ? """<item id="cover-image" href="cover.png" media-type="image/png" properties="cover-image"/>"""
            : string.Empty;

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
            <item id="chapter1" href="chapter1.xhtml" media-type="application/xhtml+xml"/>
            <item id="chapter2" href="chapter2.xhtml" media-type="application/xhtml+xml"/>
            {coverManifestItem}
          </manifest>
          <spine>
            <itemref idref="chapter1"/>
            <itemref idref="chapter2"/>
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
