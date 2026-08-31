using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class HoverPreviewJobQueueTests
{
    [Fact]
    public void Enqueue_beyond_capacity_does_not_block_and_reports_failure()
    {
        var queue = CreateQueue(capacity: 1);

        Assert.True(queue.TryEnqueue(CreateJob("a")));
        Assert.False(queue.TryEnqueue(CreateJob("b")));
    }

    [Fact]
    public void Duplicate_queued_or_running_keys_are_rejected()
    {
        var queue = CreateQueue(capacity: 4);
        var job = CreateJob("same-key");

        Assert.True(queue.TryEnqueue(job));
        Assert.False(queue.TryEnqueue(job));
        Assert.True(queue.IsActive("same-key"));
    }

    [Fact]
    public async Task Release_after_completion_allows_the_key_to_be_enqueued_again()
    {
        var queue = CreateQueue(capacity: 4);
        var job = CreateJob("key");

        Assert.True(queue.TryEnqueue(job));
        _ = await queue.DequeueAsync(CancellationToken.None);
        queue.Release("key");

        Assert.False(queue.IsActive("key"));
        Assert.True(queue.TryEnqueue(job));
    }

    [Fact]
    public void Failed_admission_releases_the_active_key_claim()
    {
        var queue = CreateQueue(capacity: 1);
        Assert.True(queue.TryEnqueue(CreateJob("first")));

        Assert.False(queue.TryEnqueue(CreateJob("second")));
        Assert.False(queue.IsActive("second"));
    }

    [Fact]
    public async Task Dequeue_honors_cancellation()
    {
        var queue = CreateQueue(capacity: 4);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.DequeueAsync(cts.Token));
    }

    [Fact]
    public async Task Admitted_jobs_are_dequeued_in_fifo_order()
    {
        var queue = CreateQueue(capacity: 4);
        Assert.True(queue.TryEnqueue(CreateJob("first")));
        Assert.True(queue.TryEnqueue(CreateJob("second")));

        var first = await queue.DequeueAsync(CancellationToken.None);
        var second = await queue.DequeueAsync(CancellationToken.None);

        Assert.Equal("first", first.CacheKey);
        Assert.Equal("second", second.CacheKey);
    }

    private static HoverPreviewJobQueue CreateQueue(int capacity) =>
        new(Options.Create(new HoverPreviewOptions { QueueCapacity = capacity }));

    private static HoverPreviewJob CreateJob(string cacheKey) =>
        new(cacheKey, new VideoFileEntry(
            Guid.NewGuid().ToString("N"),
            "/videos/clip.mp4",
            "clip.mp4",
            "clip.mp4",
            ".mp4",
            1,
            DateTime.UtcNow));
}
