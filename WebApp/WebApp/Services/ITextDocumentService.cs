using WebApp.Client.Models;
using WebApp.Models;

namespace WebApp.Services;

internal interface ITextDocumentService
{
    TextDocumentLoadResult Load(ArchiveItemEntry item);

    string RenderPreview(ArchiveItemEntry item, string source);

    TextDocumentSaveResult Save(ArchiveItemEntry item, string source, string revision);
}

internal sealed record TextDocumentLoadResult(string Source, string Revision, string PreviewHtml, TextDocumentKind DocumentKind);

internal sealed record TextDocumentSaveResult(string Revision, string PreviewHtml, TextDocumentKind DocumentKind);

internal class TextDocumentException(string message) : Exception(message);

internal sealed class TextDocumentValidationException(string message) : TextDocumentException(message);

internal sealed class TextDocumentConflictException(string message) : TextDocumentException(message);
