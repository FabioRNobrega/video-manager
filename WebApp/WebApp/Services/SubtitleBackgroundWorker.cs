using WebApp.Models;

namespace WebApp.Services;

internal sealed class SubtitleBackgroundWorker(
    ISubtitleJobQueue queue,
    ISubtitleGenerator generator,
    SubtitleCoordinator coordinator,
    IVideoLibraryService library,
    ILogger<SubtitleBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            SubtitleJob job;
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
                "Subtitle conversion started for media {MediaId} (key {KeyPrefix}).", job.SourceEntry.Id, keyPrefix);

            try
            {
                var destination = coordinator.GetFinalPath(job.SourceEntry);
                var result = await generator.GenerateAsync(job.SourceEntry, job.Subtitle, destination, stoppingToken);

                switch (result.Status)
                {
                    case SubtitleGenerationStatus.Success:
                        logger.LogInformation(
                            "Subtitle conversion succeeded for media {MediaId} (key {KeyPrefix}).",
                            job.SourceEntry.Id, keyPrefix);
                        break;
                    case SubtitleGenerationStatus.Cancelled:
                        logger.LogInformation(
                            "Subtitle conversion cancelled for media {MediaId} (key {KeyPrefix}).",
                            job.SourceEntry.Id, keyPrefix);
                        break;
                    default:
                        coordinator.MarkFailed(job.CacheKey);
                        logger.LogWarning(
                            "Subtitle conversion failed for media {MediaId} (key {KeyPrefix}): {Diagnostic}",
                            job.SourceEntry.Id, keyPrefix, result.Diagnostic);
                        break;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                coordinator.MarkFailed(job.CacheKey);
                logger.LogError(
                    exception,
                    "Subtitle conversion threw for media {MediaId} (key {KeyPrefix}).",
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
