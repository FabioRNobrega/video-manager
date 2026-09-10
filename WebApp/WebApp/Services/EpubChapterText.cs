using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace WebApp.Services;

internal static partial class EpubChapterText
{
    public static string Normalize(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(EpubXhtmlRepair.FixSelfClosedLiteralTags(html));
        return Whitespace().Replace(HtmlEntity.DeEntitize(document.DocumentNode.InnerText), " ").Trim();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
