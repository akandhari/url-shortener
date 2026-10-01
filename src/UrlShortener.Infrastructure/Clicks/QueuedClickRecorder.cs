using Microsoft.Extensions.Logging;
using UrlShortener.Core.Links;

namespace UrlShortener.Infrastructure.Clicks;

/// <summary>Records clicks by queueing them for <see cref="ClickWriterService"/>. Never blocks the redirect.</summary>
internal sealed partial class QueuedClickRecorder(ClickBuffer buffer, ILogger<QueuedClickRecorder> logger) : IClickRecorder
{
    public void Record(ClickEvent click)
    {
        if (!buffer.TryEnqueue(click))
        {
            LogDropped(logger, click.LinkId, buffer.DroppedCount);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Click queue full: dropped a click for link {LinkId} ({DroppedTotal} dropped since start-up)")]
    private static partial void LogDropped(ILogger logger, long linkId, long droppedTotal);
}
