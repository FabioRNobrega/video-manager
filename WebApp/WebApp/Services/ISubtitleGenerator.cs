using WebApp.Models;

namespace WebApp.Services;

internal interface ISubtitleGenerator
{
    Task<SubtitleGenerationResult> GenerateAsync(
        VideoFileEntry source,
        SubtitleFileInfo subtitle,
        string destinationPath,
        CancellationToken cancellationToken);
}
