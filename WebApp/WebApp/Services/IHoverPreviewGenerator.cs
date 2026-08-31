using WebApp.Models;

namespace WebApp.Services;

internal interface IHoverPreviewGenerator
{
    Task<HoverPreviewGenerationResult> GenerateAsync(
        VideoFileEntry source,
        string destinationPath,
        CancellationToken cancellationToken);
}
