using System.Text.RegularExpressions;

namespace WebApp.Services;

internal sealed partial class ImageCropNamingService
{
    public string GetNextPath(string outputDirectory, string sourceFileName, string extension)
    {
        var prefix = GetPrefix(sourceFileName);
        var next = Directory.EnumerateFiles(outputDirectory, $"*{extension}", SearchOption.TopDirectoryOnly)
            .Select(path => TryReadCounter(prefix, Path.GetFileNameWithoutExtension(path)))
            .Where(counter => counter is not null)
            .DefaultIfEmpty(0)
            .Max()!.Value + 1;

        return Path.Combine(outputDirectory, $"{prefix} {next:0000}{extension}");
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
