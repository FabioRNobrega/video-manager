using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class SubtitleJobQueueTests
{
    [Fact]
    public async Task Duplicate_jobs_are_rejected_until_released()
    {
        var queue = new SubtitleJobQueue(Options.Create(new ThumbnailCacheOptions { Path = "/tmp", QueueCapacity = 2 }));
        var job = new SubtitleJob("same", CreateEntry("clip.mp4"), new SubtitleFileInfo("/videos/clip.srt", 1, DateTime.UtcNow));

        Assert.True(queue.TryEnqueue(job));
        Assert.False(queue.TryEnqueue(job));
        Assert.True(queue.IsActive("same"));

        var dequeued = await queue.DequeueAsync(CancellationToken.None);
        Assert.Equal(job, dequeued);

        queue.Release("same");
        Assert.False(queue.IsActive("same"));
        Assert.True(queue.TryEnqueue(job));
    }

    private static VideoFileEntry CreateEntry(string relativePath) => new(
        Guid.NewGuid().ToString("N"),
        $"/videos/{relativePath}",
        relativePath,
        relativePath,
        Path.GetExtension(relativePath),
        100,
        DateTime.UtcNow);
}
