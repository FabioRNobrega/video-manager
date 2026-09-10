using System.Text.RegularExpressions;

namespace WebApp.Services;

// HtmlAgilityPack treats these as "literal text" elements and does not honor
// self-closing (`<tag/>`) syntax for them, even though it's valid XHTML. Without a
// matching literal `</tag>` later in the document, HAP swallows everything after the
// self-closed tag. Rewriting them to explicit open/close pairs before parsing avoids that.
internal static partial class EpubXhtmlRepair
{
    public static string FixSelfClosedLiteralTags(string html) =>
        SelfClosedLiteralTag().Replace(html, "<$1$2></$1>");

    [GeneratedRegex(@"<(title|script|style|textarea|noscript)([^>]*?)/>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SelfClosedLiteralTag();
}
