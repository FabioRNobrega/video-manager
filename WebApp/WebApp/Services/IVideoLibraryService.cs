using WebApp.Models;

namespace WebApp.Services;

internal interface IVideoLibraryService
{
    Task<IReadOnlyList<VideoFileEntry>> ScanAsync(CancellationToken cancellationToken = default);

    bool TryResolve(string id, out VideoFileEntry? entry);
}
