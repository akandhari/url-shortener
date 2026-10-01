namespace UrlShortener.Core.Links;

/// <summary>
/// One followed short link. Append-only: clicks are added, never updated, so concurrent visitors can't overwrite
/// each other. Stores the referring site's host only (not the full referrer URL, no IP address) for privacy.
/// </summary>
public sealed class ClickEvent
{
    public const int MaxReferrerHostLength = 253;   // the maximum length of a DNS host name

    public ClickEvent(long linkId, DateTimeOffset occurredAt, string? referrerHost)
    {
        LinkId = linkId;
        OccurredAt = occurredAt;
        ReferrerHost = referrerHost;
    }

    public long Id { get; private set; }

    public long LinkId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Host of the Referer header (e.g. news.example.com); null when the visitor came directly.</summary>
    public string? ReferrerHost { get; private set; }
}
