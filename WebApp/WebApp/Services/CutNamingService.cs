using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WebApp.Configuration;

namespace WebApp.Services;

internal sealed partial class CutNamingService(IOptions<VideoCutOptions> options)
{
    private readonly string _cutRoot = Path.GetFullPath(options.Value.Path);

    public string GetNextPath(string sourceFileName)
    {
        var prefix = GetPrefix(sourceFileName);
        var next = Directory.EnumerateFiles(_cutRoot, "*.mp4", SearchOption.TopDirectoryOnly)
            .Select(path => TryReadCounter(prefix, Path.GetFileNameWithoutExtension(path)))
            .Where(counter => counter is not null)
            .DefaultIfEmpty(0)
            .Max()!.Value + 1;

        return Path.Combine(_cutRoot, $"{prefix} {next:0000}.mp4");
    }

    internal static string GetPrefix(string sourceFileName)
    {
        var stem = Path.GetFileNameWithoutExtension(sourceFileName);
        var words = WhitespaceRegex().Split(stem.Trim())
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Take(2)
            .ToArray();

        return words.Length == 0 ? "Cut" : string.Join(' ', words);
    }

    private static int? TryReadCounter(string prefix, string candidate)
    {
        if (!candidate.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var counterText = candidate[(prefix.Length + 1)..];
        return counterText.Length == 4 && int.TryParse(counterText, out var counter) ? counter : null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
