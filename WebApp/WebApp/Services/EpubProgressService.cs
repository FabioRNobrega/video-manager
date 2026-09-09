using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApp.Client.Models;
using WebApp.Configuration;

namespace WebApp.Services;

internal sealed class EpubProgressService(IOptions<ArchiveRootOptions> options) : IEpubProgressService
{
    private const string ProgressFileName = "pereneArchiveBookProgress.json";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _archiveRootPath = Path.GetFullPath(options.Value.Path);
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public async Task<BookProgressDto?> LoadProgressAsync(
        string categoryKey, string itemId, long? sizeBytes, DateTime lastWriteTimeUtc, CancellationToken cancellationToken)
    {
        var key = ComputeKey(categoryKey, itemId, sizeBytes, lastWriteTimeUtc);
        await _fileLock.WaitAsync(cancellationToken);
        Dictionary<string, ProgressRecord> entries;
        try
        {
            entries = await ReadAllUnlockedAsync(cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }

        return entries.TryGetValue(key, out var record)
            ? new BookProgressDto(record.ChapterId, record.ScrollFraction)
            : null;
    }

    public async Task SaveProgressAsync(
        string categoryKey,
        string itemId,
        long? sizeBytes,
        DateTime lastWriteTimeUtc,
        BookProgressDto progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var key = ComputeKey(categoryKey, itemId, sizeBytes, lastWriteTimeUtc);

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadAllUnlockedAsync(cancellationToken);
            entries[key] = new ProgressRecord(progress.ChapterId, progress.ScrollFraction);
            await WriteAllUnlockedAsync(entries, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<Dictionary<string, ProgressRecord>> ReadAllUnlockedAsync(CancellationToken cancellationToken)
    {
        var path = GetProgressFilePath();
        if (!File.Exists(path))
        {
            return new Dictionary<string, ProgressRecord>(StringComparer.Ordinal);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var entries = await JsonSerializer.DeserializeAsync<Dictionary<string, ProgressRecord>>(
                stream, cancellationToken: cancellationToken);
            return entries ?? new Dictionary<string, ProgressRecord>(StringComparer.Ordinal);
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException or FormatException)
        {
            return new Dictionary<string, ProgressRecord>(StringComparer.Ordinal);
        }
    }

    private async Task WriteAllUnlockedAsync(Dictionary<string, ProgressRecord> entries, CancellationToken cancellationToken)
    {
        var notesFolder = Path.Combine(_archiveRootPath, "Books", "Notes");
        Directory.CreateDirectory(notesFolder);
        var path = Path.Combine(notesFolder, ProgressFileName);
        var tempPath = Path.Combine(notesFolder, $".{ProgressFileName}.{Guid.NewGuid():N}.tmp");

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, entries, SerializerOptions, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private string GetProgressFilePath() => Path.Combine(_archiveRootPath, "Books", "Notes", ProgressFileName);

    private static string ComputeKey(string categoryKey, string itemId, long? sizeBytes, DateTime lastWriteTimeUtc)
    {
        var identity = $"{categoryKey}:{itemId}:{sizeBytes ?? 0}:{lastWriteTimeUtc.Ticks}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    private sealed record ProgressRecord(string ChapterId, double ScrollFraction);
}
