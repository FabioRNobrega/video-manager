using System.Threading.Channels;
using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class CutJobQueue : ICutJobQueue
{
    private readonly Channel<CutJob> _channel;

    public CutJobQueue(IOptions<VideoCutOptions> options)
    {
        var capacity = Math.Max(1, options.Value.QueueCapacity);
        _channel = Channel.CreateBounded<CutJob>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public bool TryEnqueue(CutJob job) => _channel.Writer.TryWrite(job);

    public async Task<CutJob> DequeueAsync(CancellationToken cancellationToken) =>
        await _channel.Reader.ReadAsync(cancellationToken);
}
