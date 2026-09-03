using WebApp.Models;

namespace WebApp.Services;

internal interface ICutGenerator
{
    Task<CutGenerationResult> GenerateAsync(CutJob job, CancellationToken cancellationToken);
}
