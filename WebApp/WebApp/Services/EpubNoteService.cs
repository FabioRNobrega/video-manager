using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using WebApp.Configuration;

namespace WebApp.Services;

internal sealed class EpubNoteService(IOptions<ArchiveRootOptions> options) : IEpubNoteService
{
    private const string NotesFileName = "pereneArchiveBookNotes.txt";

    private readonly string _archiveRootPath = Path.GetFullPath(options.Value.Path);
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task AppendNoteAsync(
        string bookTitle,
        string? bookAuthor,
        int chapterIndex,
        int? textOffsetStart,
        int? textOffsetEnd,
        string selectedText,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedText);

        var entry = BuildEntry(bookTitle, bookAuthor, chapterIndex, textOffsetStart, textOffsetEnd, selectedText);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var notesFolder = Path.Combine(_archiveRootPath, "Books", "Notes");
            Directory.CreateDirectory(notesFolder);
            var notesFilePath = Path.Combine(notesFolder, NotesFileName);
            await File.AppendAllTextAsync(notesFilePath, entry, Encoding.UTF8, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static string BuildEntry(
        string bookTitle,
        string? bookAuthor,
        int chapterIndex,
        int? textOffsetStart,
        int? textOffsetEnd,
        string selectedText)
    {
        var titleLine = string.IsNullOrWhiteSpace(bookAuthor)
            ? bookTitle
            : $"{bookTitle} ({bookAuthor})";

        var location = textOffsetStart is not null && textOffsetEnd is not null
            ? $"Location {textOffsetStart.Value}-{textOffsetEnd.Value}"
            : $"Location {(chapterIndex + 1) * 1000}";

        var timestamp = DateTime.Now.ToString("dddd, d MMMM yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        var metadataLine = $"- Your Highlight on Chapter {chapterIndex + 1} | {location} | Added on {timestamp}";

        var builder = new StringBuilder();
        builder.AppendLine(titleLine);
        builder.AppendLine(metadataLine);
        builder.AppendLine();
        builder.AppendLine(selectedText.Trim());
        builder.AppendLine("==========");
        return builder.ToString();
    }
}
