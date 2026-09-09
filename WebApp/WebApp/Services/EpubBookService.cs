using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using VersOne.Epub;
using WebApp.Client.Models;
using WebApp.Models;

namespace WebApp.Services;

internal sealed partial class EpubBookService(IEpubContentSanitizer sanitizer) : IEpubBookService
{
    public bool TryGetBook(ArchiveItemEntry item, out BookDto? book)
    {
        book = null;
        if (!TryReadBook(item, out var epubBook) || epubBook is null)
        {
            return false;
        }

        var chapterIds = Enumerable.Range(0, epubBook.ReadingOrder.Count)
            .Select(ToChapterId)
            .ToList();
        var chapterIndexByPath = BuildChapterIndexByPath(epubBook);
        var navigation = epubBook.Navigation is null
            ? new List<BookNavigationItemDto>()
            : BuildNavigation(epubBook.Navigation, chapterIndexByPath);
        var chapters = BuildChapterProgressMetadata(epubBook);
        var hasCover = epubBook.CoverImage is { Length: > 0 };
        var title = string.IsNullOrWhiteSpace(epubBook.Title) ? item.Name : epubBook.Title;
        var author = string.IsNullOrWhiteSpace(epubBook.Author) ? null : epubBook.Author;

        book = new BookDto(item.Id, title, author, hasCover, null, navigation, chapterIds, chapters, chapters.Sum(chapter => chapter.WordCount), null);
        return true;
    }

    public bool TryGetChapter(ArchiveItemEntry item, string chapterId, out BookChapterDto? chapter)
    {
        chapter = null;
        if (!TryParseChapterIndex(chapterId, out var index))
        {
            return false;
        }

        if (!TryReadBook(item, out var epubBook) || epubBook is null || index >= epubBook.ReadingOrder.Count)
        {
            return false;
        }

        var contentFile = epubBook.ReadingOrder[index];
        var sanitizedHtml = sanitizer.Sanitize(contentFile.Content);
        var normalizedText = EpubChapterText.Normalize(sanitizedHtml);
        var previousId = index > 0 ? ToChapterId(index - 1) : null;
        var nextId = index < epubBook.ReadingOrder.Count - 1 ? ToChapterId(index + 1) : null;
        var title = epubBook.Navigation is null ? null : FindNavigationTitle(epubBook.Navigation, contentFile.FilePath);

        var wordCount = CountWordsFromHtml(sanitizedHtml);
        chapter = new BookChapterDto(ToChapterId(index), index, title, sanitizedHtml, normalizedText, wordCount, previousId, nextId);
        return true;
    }

    public bool TryGetCover(ArchiveItemEntry item, out byte[]? coverBytes, out string? contentType)
    {
        coverBytes = null;
        contentType = null;
        if (!TryReadBook(item, out var epubBook) || epubBook?.CoverImage is not { Length: > 0 } cover)
        {
            return false;
        }

        coverBytes = cover;
        contentType = DetectImageContentType(cover);
        return true;
    }

    private static bool TryReadBook(ArchiveItemEntry item, out EpubBook? book)
    {
        try
        {
            book = EpubReader.ReadBook(item.PhysicalPath);
            return true;
        }
        catch (Exception exception) when (
            exception is EpubReaderException or IOException or UnauthorizedAccessException
                or FileNotFoundException or DirectoryNotFoundException or InvalidDataException or NotSupportedException)
        {
            book = null;
            return false;
        }
    }

    private static bool TryParseChapterIndex(string chapterId, out int index) =>
        int.TryParse(chapterId, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index >= 0;

    private static string ToChapterId(int index) => index.ToString(CultureInfo.InvariantCulture);

    private List<BookChapterProgressMetadataDto> BuildChapterProgressMetadata(EpubBook book)
    {
        var chapters = new List<BookChapterProgressMetadataDto>(book.ReadingOrder.Count);
        for (var index = 0; index < book.ReadingOrder.Count; index++)
        {
            var sanitizedHtml = sanitizer.Sanitize(book.ReadingOrder[index].Content);
            chapters.Add(new BookChapterProgressMetadataDto(ToChapterId(index), index, CountWordsFromHtml(sanitizedHtml)));
        }

        return chapters;
    }

    internal static int CountWordsFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return 0;
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);
        var text = HtmlEntity.DeEntitize(document.DocumentNode.InnerText);
        return WordRegex().Count(text);
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’-][\p{L}\p{N}]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    private static Dictionary<string, int> BuildChapterIndexByPath(EpubBook book)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < book.ReadingOrder.Count; index++)
        {
            var path = book.ReadingOrder[index].FilePath;
            map.TryAdd(path, index);
        }

        return map;
    }

    private static List<BookNavigationItemDto> BuildNavigation(
        List<EpubNavigationItem> items, Dictionary<string, int> chapterIndexByPath)
    {
        var result = new List<BookNavigationItemDto>();
        foreach (var navigationItem in items)
        {
            var chapterId = navigationItem.Link is not null &&
                chapterIndexByPath.TryGetValue(navigationItem.Link.ContentFilePath, out var index)
                    ? ToChapterId(index)
                    : null;
            var children = BuildNavigation(navigationItem.NestedItems, chapterIndexByPath);
            result.Add(new BookNavigationItemDto(navigationItem.Title, chapterId, children));
        }

        return result;
    }

    private static string? FindNavigationTitle(List<EpubNavigationItem> items, string filePath)
    {
        foreach (var navigationItem in items)
        {
            if (navigationItem.Link is not null &&
                string.Equals(navigationItem.Link.ContentFilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return navigationItem.Title;
            }

            var nested = FindNavigationTitle(navigationItem.NestedItems, filePath);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static string DetectImageContentType(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 3 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F')
        {
            return "image/gif";
        }

        return "application/octet-stream";
    }
}
