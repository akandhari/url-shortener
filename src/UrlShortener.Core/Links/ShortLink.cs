namespace UrlShortener.Core.Links;

public enum LinkStatus
{
    Active,
    Expired,
    Disabled,
}

public sealed class ShortLink
{
    public ShortLink(string code, Uri targetUrl, DateTimeOffset createdAt, DateTimeOffset? expiresAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(targetUrl);

        Code = code;
        TargetUrl = targetUrl;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>Database identity, assigned on insert.</summary>
    public long Id { get; private set; }

    public string Code { get; private set; }

    public Uri TargetUrl { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Total clicks, increased by the database as click events are stored (eventually consistent).</summary>
    public long ClickCount { get; private set; }

    /// <summary>After this moment the link answers 410 Gone (AB-04). Null: never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>When the link was taken down (AB-04). Null: not disabled.</summary>
    public DateTimeOffset? DisabledAt { get; private set; }

    public LinkStatus StatusAt(DateTimeOffset now) => LinkStatusRules.Of(DisabledAt is not null, ExpiresAt, now);
}

internal static class LinkStatusRules
{
    // Disabled wins over expired: it is the stronger, deliberate signal.
    public static LinkStatus Of(bool disabled, DateTimeOffset? expiresAt, DateTimeOffset now) =>
        disabled ? LinkStatus.Disabled
        : expiresAt is { } expiry && now >= expiry ? LinkStatus.Expired
        : LinkStatus.Active;
}
