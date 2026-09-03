using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class CutJobQueueTests
{
    [Fact]
    public async Task Queue_preserves_fifo_order()
    {
        var queue = new CutJobQueue(Options.Create(new VideoCutOptions { Path = "/", QueueCapacity = 2 }));
        var first = CreateJob("first");
        var second = CreateJob("second");

        Assert.True(queue.TryEnqueue(first));
        Assert.True(queue.TryEnqueue(second));

        Assert.Equal(first, await queue.DequeueAsync(CancellationToken.None));
        Assert.Equal(second, await queue.DequeueAsync(CancellationToken.None));
    }

    private static CutJob CreateJob(string jobId) =>
        new(jobId, new VideoFileEntry(Guid.NewGuid().ToString("N"), "/videos/source.mp4", "source.mp4",
            "source.mp4", ".mp4", 1, DateTime.UtcNow), TimeSpan.Zero, TimeSpan.FromSeconds(1));
}
