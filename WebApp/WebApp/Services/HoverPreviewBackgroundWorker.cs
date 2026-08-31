using WebApp.Models;

namespace WebApp.Services;

internal sealed class HoverPreviewBackgroundWorker(
    IHoverPreviewJobQueue queue,
    IHoverPreviewGenerator generator,
    HoverPreviewCoordinator coordinator,
    IVideoLibraryService library,
    ILogger<HoverPreviewBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            HoverPreviewJob job;
            try
            {
                job = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var keyPrefix = job.CacheKey[..Math.Min(12, job.CacheKey.Length)];
            logger.LogInformation(
                "Hover preview generation started for media {MediaId} (key {KeyPrefix}).", job.SourceEntry.Id, keyPrefix);

            try
            {
                var destination = coordinator.GetFinalPath(job.SourceEntry);
                var result = await generator.GenerateAsync(job.SourceEntry, destination, stoppingToken);

                switch (result.Status)
                {
                    case HoverPreviewGenerationStatus.Success:
                        logger.LogInformation(
                            "Hover preview generation succeeded for media {MediaId} (key {KeyPrefix}).",
                            job.SourceEntry.Id, keyPrefix);
                        break;
                    case HoverPreviewGenerationStatus.Cancelled:
                        logger.LogInformation(
                            "Hover preview generation cancelled for media {MediaId} (key {KeyPrefix}).",
                            job.SourceEntry.Id, keyPrefix);
                        break;
                    default:
                        coordinator.MarkFailed(job.CacheKey);
                        logger.LogWarning(
                            "Hover preview generation failed for media {MediaId} (key {KeyPrefix}): {Diagnostic}",
                            job.SourceEntry.Id, keyPrefix, result.Diagnostic);
                        break;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                coordinator.MarkFailed(job.CacheKey);
                logger.LogError(
                    exception,
                    "Hover preview generation threw for media {MediaId} (key {KeyPrefix}).",
                    job.SourceEntry.Id, keyPrefix);
            }
            finally
            {
                queue.Release(job.CacheKey);
            }

            coordinator.Reconcile(library.GetCurrentSnapshot());
        }
    }
}
