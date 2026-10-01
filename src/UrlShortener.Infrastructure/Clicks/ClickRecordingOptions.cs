namespace UrlShortener.Infrastructure.Clicks;

public sealed class ClickRecordingOptions
{
    public const string SectionName = "ClickRecording";

    /// <summary>
    /// Maximum clicks waiting to be written. Bounded so a burst can't exhaust memory; when full, new clicks are dropped.
    /// </summary>
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>Maximum clicks written in one database transaction.</summary>
    public int MaxBatchSize { get; set; } = 500;
}
