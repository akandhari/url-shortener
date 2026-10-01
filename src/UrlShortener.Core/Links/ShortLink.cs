namespace UrlShortener.Core.Links;

public sealed class ShortLink
{
    public ShortLink(string code, Uri targetUrl, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(targetUrl);

        Code = code;
        TargetUrl = targetUrl;
        CreatedAt = createdAt;
    }

    /// <summary>Database identity, assigned on insert.</summary>
    public long Id { get; private set; }

    public string Code { get; private set; }

    public Uri TargetUrl { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Total clicks, increased by the database as click events are stored (eventually consistent).</summary>
    public long ClickCount { get; private set; }
}
