using System.Threading.Channels;
using Microsoft.Extensions.Options;
using WebApp.Configuration;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class SubtitleJobQueue : ISubtitleJobQueue
{
    private readonly Channel<SubtitleJob> _channel;
    private readonly HashSet<string> _activeKeys = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public SubtitleJobQueue(IOptions<ThumbnailCacheOptions> options)
    {
        var capacity = Math.Max(1, options.Value.QueueCapacity);
        _channel = Channel.CreateBounded<SubtitleJob>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public bool TryEnqueue(SubtitleJob job)
    {
        lock (_gate)
        {
            if (!_activeKeys.Add(job.CacheKey))
            {
                return false;
            }
        }

        if (_channel.Writer.TryWrite(job))
        {
            return true;
        }

        lock (_gate)
        {
            _activeKeys.Remove(job.CacheKey);
        }

        return false;
    }

    public async Task<SubtitleJob> DequeueAsync(CancellationToken cancellationToken) =>
        await _channel.Reader.ReadAsync(cancellationToken);

    public bool IsActive(string cacheKey)
    {
        lock (_gate)
        {
            return _activeKeys.Contains(cacheKey);
        }
    }

    public void Release(string cacheKey)
    {
        lock (_gate)
        {
            _activeKeys.Remove(cacheKey);
        }
    }

    public int ActiveCount
    {
        get
        {
            lock (_gate)
            {
                return _activeKeys.Count;
            }
        }
    }
}
