namespace WebApp.Services;

internal interface IEpubContentSanitizer
{
    /// <summary>
    /// Sanitizes raw EPUB chapter HTML into a safe, app-controlled subset suitable for rendering in the browser.
    /// Strips scripts, event handler attributes, iframes, forms, images, remote resource references, and publisher
    /// CSS/styling while preserving safe semantic formatting (headings, paragraphs, emphasis, lists, block quotes,
    /// and same-document anchors).
    /// </summary>
    string Sanitize(string html);
}
