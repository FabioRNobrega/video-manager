using WebApp.Models;

namespace WebApp.Services;

internal sealed class ThumbnailBackgroundWorker(
    IThumbnailJobQueue queue,
    IThumbnailGenerator generator,
    ThumbnailCoordinator coordinator,
    IVideoLibraryService library,
    ILogger<ThumbnailBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ThumbnailJob job;
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
                "Thumbnail generation started for media {MediaId} (key {KeyPrefix}).", job.SourceEntry.Id, keyPrefix);

            try
            {
                var destination = coordinator.GetFinalPath(job.SourceEntry);
                var result = await generator.GenerateAsync(job.SourceEntry, destination, stoppingToken);

                switch (result.Status)
                {
                    case ThumbnailGenerationStatus.Success:
                        logger.LogInformation(
                            "Thumbnail generation succeeded for media {MediaId} (key {KeyPrefix}).",
                            job.SourceEntry.Id, keyPrefix);
                        break;
                    case ThumbnailGenerationStatus.Cancelled:
                        logger.LogInformation(
                            "Thumbnail generation cancelled for media {MediaId} (key {KeyPrefix}).",
                            job.SourceEntry.Id, keyPrefix);
                        break;
                    default:
                        coordinator.MarkFailed(job.CacheKey);
                        logger.LogWarning(
                            "Thumbnail generation failed for media {MediaId} (key {KeyPrefix}): {Diagnostic}",
                            job.SourceEntry.Id, keyPrefix, result.Diagnostic);
                        break;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                coordinator.MarkFailed(job.CacheKey);
                logger.LogError(
                    exception,
                    "Thumbnail generation threw for media {MediaId} (key {KeyPrefix}).",
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
