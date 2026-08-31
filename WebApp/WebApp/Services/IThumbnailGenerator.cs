using WebApp.Models;

namespace WebApp.Services;

internal interface IThumbnailGenerator
{
    Task<ThumbnailGenerationResult> GenerateAsync(
        VideoFileEntry source,
        string destinationPath,
        CancellationToken cancellationToken);
}
