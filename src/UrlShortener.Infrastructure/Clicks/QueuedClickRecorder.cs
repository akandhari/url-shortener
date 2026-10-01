using Microsoft.Extensions.Logging;
using UrlShortener.Core.Links;

namespace UrlShortener.Infrastructure.Clicks;

/// <summary>Records clicks by queueing them for <see cref="ClickWriterService"/>. Never blocks the redirect.</summary>
internal sealed partial class QueuedClickRecorder(ClickBuffer buffer, ILogger<QueuedClickRecorder> logger) : IClickRecorder
{
    // Log the first drop and then one summary per this many drops. One line per dropped click flooded the log with
    // 63,570 warnings in a 30-second load test (WR-02), turning the log itself into a cost under a burst.
    internal const long LogEveryNthDrop = 1_000;

    public void Record(ClickEvent click)
    {
        if (buffer.TryEnqueue(click))
        {
            return;
        }

        var dropped = buffer.DroppedCount;
        if (dropped == 1 || dropped % LogEveryNthDrop == 0)
        {
            LogDropped(logger, dropped);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Click buffer full: clicks are being dropped ({DroppedTotal} dropped since start-up)")]
    private static partial void LogDropped(ILogger logger, long droppedTotal);
}
