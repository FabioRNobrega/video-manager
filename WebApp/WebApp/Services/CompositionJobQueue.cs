using System.Threading.Channels;
using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class CompositionJobQueue : ICompositionJobQueue
{
    private readonly Channel<CompositionJob> _channel;

    public CompositionJobQueue(IOptions<VideoCompositionOptions> options)
    {
        var capacity = Math.Max(1, options.Value.QueueCapacity);
        _channel = Channel.CreateBounded<CompositionJob>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public bool TryEnqueue(CompositionJob job) => _channel.Writer.TryWrite(job);

    public async Task<CompositionJob> DequeueAsync(CancellationToken cancellationToken) =>
        await _channel.Reader.ReadAsync(cancellationToken);
}
