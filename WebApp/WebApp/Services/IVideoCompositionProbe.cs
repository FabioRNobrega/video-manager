using WebApp.Models;

namespace WebApp.Services;

internal interface IVideoCompositionProbe
{
    Task<CompositionInputProbe?> ProbeAsync(string physicalPath, CancellationToken cancellationToken);
}
