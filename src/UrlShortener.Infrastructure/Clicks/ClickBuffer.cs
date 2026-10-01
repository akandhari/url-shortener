using System.Threading.Channels;
using Microsoft.Extensions.Options;
using UrlShortener.Core.Links;

namespace UrlShortener.Infrastructure.Clicks;

/// <summary>
/// In-memory, bounded buffer between the redirect (producer) and the background writer (single consumer).
/// </summary>
public sealed class ClickBuffer
{
    private readonly Channel<ClickEvent> _channel;
    private long _dropped;

    public ClickBuffer(IOptions<ClickRecordingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _channel = Channel.CreateBounded<ClickEvent>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            // Never block the redirect: when full, the new click is rejected (TryWrite returns false).
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ChannelReader<ClickEvent> Reader => _channel.Reader;

    /// <summary>Clicks rejected because the queue was full, since start-up.</summary>
    public long DroppedCount => Interlocked.Read(ref _dropped);

    /// <summary>Queues a click without waiting. Returns false (and counts a drop) when the queue is full.</summary>
    public bool TryEnqueue(ClickEvent click)
    {
        if (_channel.Writer.TryWrite(click))
        {
            return true;
        }

        Interlocked.Increment(ref _dropped);
        return false;
    }
}
