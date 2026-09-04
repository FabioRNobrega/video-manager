using Microsoft.Extensions.Options;
using WebApp.Configuration;

namespace WebApp.Services;

internal sealed class CompositionNamingService(IOptions<VideoCompositionOptions> options)
{
    private readonly string _compositionRoot = Path.GetFullPath(options.Value.Path);

    public string GetNextPath(string firstSourceFileName)
    {
        var prefix = CutNamingService.GetPrefix(firstSourceFileName);
        var next = Directory.EnumerateFiles(_compositionRoot, "*.mp4", SearchOption.TopDirectoryOnly)
            .Select(path => TryReadCounter(prefix, Path.GetFileNameWithoutExtension(path)))
            .Where(counter => counter is not null)
            .DefaultIfEmpty(0)
            .Max()!.Value + 1;

        return Path.Combine(_compositionRoot, $"{prefix} Composition {next:0000}.mp4");
    }

    private static int? TryReadCounter(string prefix, string candidate)
    {
        var expectedPrefix = $"{prefix} Composition ";
        if (!candidate.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var counterText = candidate[expectedPrefix.Length..];
        return counterText.Length == 4 && int.TryParse(counterText, out var counter) ? counter : null;
    }
}
