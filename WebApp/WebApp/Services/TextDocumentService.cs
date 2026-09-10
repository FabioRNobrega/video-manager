using System.Security.Cryptography;
using System.Text;
using HtmlAgilityPack;
using Markdig;
using WebApp.Client.Models;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class TextDocumentService : ITextDocumentService
{
    internal const int MaxSourceBytes = 1_000_000;

    private static readonly HashSet<string> MarkdownExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".md", ".markdown" };

    private static readonly HashSet<string> RemovedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "img", "script", "iframe", "form", "object", "embed", "style", "video", "audio",
        "svg", "link", "meta", "base", "button", "textarea", "select", "option", "canvas",
        "source", "track", "applet", "frame", "frameset", "noscript"
    };

    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6", "strong", "b", "em", "i",
        "del", "s", "strike", "ins", "mark", "sub", "sup", "blockquote",
        "ul", "ol", "li", "code", "pre", "table", "thead", "tbody", "tfoot", "tr", "th", "td",
        "a", "input", "dl", "dt", "dd"
    };

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoLinks()
        .DisableHtml()
        .Build();

    public TextDocumentLoadResult Load(ArchiveItemEntry item)
    {
        var (source, sizeBytes, lastWriteTimeUtc) = ReadSource(item.PhysicalPath);
        var kind = DetermineKind(item);
        var preview = Render(kind, source);
        var revision = ComputeRevision(sizeBytes, lastWriteTimeUtc);
        return new TextDocumentLoadResult(source, revision, preview, kind);
    }

    public string RenderPreview(ArchiveItemEntry item, string source)
    {
        EnsureWithinLimit(source);
        return Render(DetermineKind(item), source);
    }

    public TextDocumentSaveResult Save(ArchiveItemEntry item, string source, string revision)
    {
        EnsureWithinLimit(source);

        var currentInfo = new FileInfo(item.PhysicalPath);
        if (!currentInfo.Exists)
        {
            throw new TextDocumentValidationException("The document no longer exists.");
        }

        var currentRevision = ComputeRevision(currentInfo.Length, currentInfo.LastWriteTimeUtc);
        if (!string.Equals(currentRevision, revision, StringComparison.Ordinal))
        {
            throw new TextDocumentConflictException("The document changed outside the editor.");
        }

        var directory = Path.GetDirectoryName(item.PhysicalPath)
            ?? throw new TextDocumentValidationException("The document location is invalid.");
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(item.PhysicalPath)}.tmp-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(tempPath, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, item.PhysicalPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        var newInfo = new FileInfo(item.PhysicalPath);
        var newRevision = ComputeRevision(newInfo.Length, newInfo.LastWriteTimeUtc);
        var kind = DetermineKind(item);
        var preview = Render(kind, source);
        return new TextDocumentSaveResult(newRevision, preview, kind);
    }

    public string SavePdfExport(ArchiveItemEntry item, byte[] pdfBytes)
    {
        var directory = Path.GetDirectoryName(item.PhysicalPath)
            ?? throw new TextDocumentValidationException("The document location is invalid.");
        var fileName = $"{Path.GetFileNameWithoutExtension(item.PhysicalPath)}.pdf";
        var targetPath = Path.Combine(directory, fileName);
        var tempPath = Path.Combine(directory, $".{fileName}.tmp-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllBytes(tempPath, pdfBytes);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        return fileName;
    }

    private static (string Source, long SizeBytes, DateTime LastWriteTimeUtc) ReadSource(string physicalPath)
    {
        var info = new FileInfo(physicalPath);
        if (!info.Exists)
        {
            throw new TextDocumentValidationException("The document no longer exists.");
        }

        if (info.Length > MaxSourceBytes)
        {
            throw new TextDocumentValidationException("The document is too large to open.");
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(physicalPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new TextDocumentValidationException("The document could not be read.");
        }

        string source;
        try
        {
            source = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new TextDocumentValidationException("The document is not valid UTF-8 text.");
        }

        return (source, info.Length, info.LastWriteTimeUtc);
    }

    private static void EnsureWithinLimit(string source)
    {
        if (Encoding.UTF8.GetByteCount(source) > MaxSourceBytes)
        {
            throw new TextDocumentValidationException("The document is too large to save.");
        }
    }

    internal static TextDocumentKind DetermineKind(ArchiveItemEntry item) =>
        item.Extension is not null && MarkdownExtensions.Contains(item.Extension)
            ? TextDocumentKind.Markdown
            : TextDocumentKind.PlainText;

    private static string Render(TextDocumentKind kind, string source) =>
        kind == TextDocumentKind.Markdown ? RenderMarkdown(source) : RenderPlainText(source);

    private static string RenderMarkdown(string source)
    {
        var html = Markdown.ToHtml(source, Pipeline);
        return Sanitize(html);
    }

    private static string RenderPlainText(string source) =>
        $"<pre class=\"text-document-plain\">{HtmlEntity.Entitize(source, true, true)}</pre>";

    private static string Sanitize(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        foreach (var node in document.DocumentNode.Descendants().ToList())
        {
            if (node.NodeType == HtmlNodeType.Comment)
            {
                node.Remove();
                continue;
            }

            if (node.NodeType != HtmlNodeType.Element)
            {
                continue;
            }

            var name = node.Name.ToLowerInvariant();
            if (RemovedTags.Contains(name))
            {
                node.Remove();
                continue;
            }

            if (!AllowedTags.Contains(name))
            {
                Unwrap(node);
                continue;
            }

            if (name == "a")
            {
                SanitizeAnchor(node);
                continue;
            }

            if (name == "input")
            {
                SanitizeCheckbox(node);
                continue;
            }

            foreach (var attribute in node.Attributes.ToList())
            {
                node.Attributes.Remove(attribute);
            }
        }

        using var writer = new StringWriter();
        document.DocumentNode.WriteTo(writer);
        return writer.ToString();
    }

    private static void SanitizeAnchor(HtmlNode node)
    {
        var href = node.GetAttributeValue("href", null);
        foreach (var attribute in node.Attributes.ToList())
        {
            node.Attributes.Remove(attribute);
        }

        if (href is not null && Uri.TryCreate(href, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            node.SetAttributeValue("href", uri.AbsoluteUri);
            node.SetAttributeValue("target", "_blank");
            node.SetAttributeValue("rel", "noopener noreferrer nofollow");
        }
        else
        {
            Unwrap(node);
        }
    }

    private static void SanitizeCheckbox(HtmlNode node)
    {
        var type = node.GetAttributeValue("type", null);
        var isChecked = node.Attributes.Contains("checked");
        foreach (var attribute in node.Attributes.ToList())
        {
            node.Attributes.Remove(attribute);
        }

        if (!string.Equals(type, "checkbox", StringComparison.OrdinalIgnoreCase))
        {
            node.Remove();
            return;
        }

        node.SetAttributeValue("type", "checkbox");
        node.SetAttributeValue("disabled", "disabled");
        if (isChecked)
        {
            node.SetAttributeValue("checked", "checked");
        }
    }

    private static void Unwrap(HtmlNode node)
    {
        var parent = node.ParentNode;
        if (parent is null)
        {
            node.Remove();
            return;
        }

        foreach (var child in node.ChildNodes.ToList())
        {
            parent.InsertBefore(child, node);
        }

        parent.RemoveChild(node);
    }

    private static string ComputeRevision(long sizeBytes, DateTime lastWriteTimeUtc) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{sizeBytes}:{lastWriteTimeUtc.Ticks}")))[..32].ToLowerInvariant();
}
