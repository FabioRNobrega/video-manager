using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class CompositionJobQueueTests
{
    [Fact]
    public async Task Queue_preserves_fifo_order()
    {
        var queue = new CompositionJobQueue(Options.Create(new VideoCompositionOptions { Path = "/", QueueCapacity = 2 }));
        var first = CreateJob("first");
        var second = CreateJob("second");

        Assert.True(queue.TryEnqueue(first));
        Assert.True(queue.TryEnqueue(second));

        Assert.Equal(first, await queue.DequeueAsync(CancellationToken.None));
        Assert.Equal(second, await queue.DequeueAsync(CancellationToken.None));
    }

    private static CompositionJob CreateJob(string jobId) =>
        new(jobId,
        [
            new VideoFileEntry(Guid.NewGuid().ToString("N"), "/videos-cuts/a.mp4", "a.mp4", "a.mp4", ".mp4", 1, DateTime.UtcNow),
            new VideoFileEntry(Guid.NewGuid().ToString("N"), "/videos-cuts/b.mp4", "b.mp4", "b.mp4", ".mp4", 1, DateTime.UtcNow),
        ]);
}
