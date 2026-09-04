using WebApp.Client.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class CompositionJobStatusStoreTests
{
    [Fact]
    public void Seeded_job_starts_pending_and_transitions_through_processing_to_completed()
    {
        var store = new CompositionJobStatusStore();
        store.Seed("job-1");

        Assert.Equal(CompositionJobState.Pending, Assert.Single(store.GetAll()).State);

        store.MarkProcessing("job-1");
        Assert.Equal(CompositionJobState.Processing, Assert.Single(store.GetAll()).State);

        store.MarkCompleted("job-1", "video-id");
        var completed = Assert.Single(store.GetAll());
        Assert.Equal(CompositionJobState.Completed, completed.State);
        Assert.Equal("video-id", completed.ResultVideoId);
    }

    [Fact]
    public void Failed_job_records_diagnostic()
    {
        var store = new CompositionJobStatusStore();
        store.Seed("job-1");

        store.MarkFailed("job-1", "ffmpeg exploded");

        var failed = Assert.Single(store.GetAll());
        Assert.Equal(CompositionJobState.Failed, failed.State);
        Assert.Equal("ffmpeg exploded", failed.Diagnostic);
    }

    [Fact]
    public void Jobs_are_returned_in_the_order_they_were_seeded()
    {
        var store = new CompositionJobStatusStore();
        store.Seed("first");
        store.Seed("second");
        store.Seed("third");

        Assert.Equal(["first", "second", "third"], store.GetAll().Select(status => status.JobId));
    }
}
