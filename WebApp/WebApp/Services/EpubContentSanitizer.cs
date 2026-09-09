using HtmlAgilityPack;

namespace WebApp.Services;

internal sealed class EpubContentSanitizer : IEpubContentSanitizer
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "h1", "h2", "h3", "h4", "h5", "h6",
        "p", "br", "hr", "blockquote",
        "ul", "ol", "li",
        "em", "i", "strong", "b", "u", "s", "sub", "sup",
        "span", "div", "a"
    };

    private static readonly HashSet<string> RemovedEntirelyTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "form", "input", "button", "select", "textarea",
        "object", "embed", "video", "audio", "canvas", "svg", "img", "picture", "source",
        "link", "meta", "head", "title", "noscript"
    };

    public string Sanitize(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var body = document.DocumentNode.SelectSingleNode("//body") ?? document.DocumentNode;
        SanitizeChildren(body);

        return body.InnerHtml.Trim();
    }

    private void SanitizeChildren(HtmlNode node)
    {
        foreach (var child in node.ChildNodes.ToList())
        {
            SanitizeNode(child);
        }
    }

    private void SanitizeNode(HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Comment)
        {
            node.Remove();
            return;
        }

        if (node.NodeType != HtmlNodeType.Element)
        {
            return;
        }

        var name = node.Name.ToLowerInvariant();

        if (RemovedEntirelyTags.Contains(name))
        {
            node.Remove();
            return;
        }

        if (!AllowedTags.Contains(name))
        {
            // Unwrap unknown/unsafe wrapper elements while keeping their sanitized text content.
            SanitizeChildren(node);
            UnwrapNode(node);
            return;
        }

        StripUnsafeAttributes(node, name);
        SanitizeChildren(node);
    }

    private static void UnwrapNode(HtmlNode node)
    {
        var parent = node.ParentNode;
        if (parent is null)
        {
            return;
        }

        foreach (var child in node.ChildNodes.ToList())
        {
            parent.InsertBefore(child, node);
        }

        node.Remove();
    }

    private static void StripUnsafeAttributes(HtmlNode node, string tagName)
    {
        foreach (var attribute in node.Attributes.ToList())
        {
            if (!IsAttributeAllowed(tagName, attribute))
            {
                node.Attributes.Remove(attribute);
            }
        }
    }

    private static bool IsAttributeAllowed(string tagName, HtmlAttribute attribute)
    {
        var name = attribute.Name.ToLowerInvariant();
        if (name.StartsWith("on", StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(tagName, "a", StringComparison.OrdinalIgnoreCase) &&
            name == "href" &&
            IsSafeAnchorHref(attribute.Value);
    }

    private static bool IsSafeAnchorHref(string? href) =>
        !string.IsNullOrWhiteSpace(href) && href.StartsWith('#');
}
