using WebApp.Models;

namespace WebApp.Services;

internal interface ICompositionGenerator
{
    Task<CompositionGenerationResult> GenerateAsync(CompositionJob job, CancellationToken cancellationToken);
}
