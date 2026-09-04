using WebApp.Models;

namespace WebApp.Services;

internal interface IVideoCompositionService
{
    Task<IReadOnlyList<VideoFileEntry>> ScanAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<VideoFileEntry> GetCurrentSnapshot();

    bool TryResolve(string id, out VideoFileEntry? entry);
}
