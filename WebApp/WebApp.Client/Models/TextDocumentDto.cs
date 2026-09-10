namespace WebApp.Client.Models;

public sealed record TextDocumentDto(
    string Id,
    string Name,
    TextDocumentKind DocumentKind,
    string Source,
    string Revision,
    string PreviewHtml);
