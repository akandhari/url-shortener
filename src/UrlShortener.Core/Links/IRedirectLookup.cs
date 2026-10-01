namespace UrlShortener.Core.Links;

/// <summary>
/// What a redirect needs: the link's id (to record the click), where to send the visitor, and whether the link is
/// still live. Expiry is kept here, so even a cached target stops working on time.
/// </summary>
public sealed record RedirectTarget(long LinkId, Uri TargetUrl, DateTimeOffset? ExpiresAt = null, bool IsDisabled = false)
{
    public LinkStatus StatusAt(DateTimeOffset now) => LinkStatusRules.Of(IsDisabled, ExpiresAt, now);
}

/// <summary>
/// Finds the redirect target for a code. Separate from <see cref="ILinkRepository"/> so the hot redirect path can be
/// cached without making the details endpoint show a stale click count.
/// </summary>
public interface IRedirectLookup
{
    Task<RedirectTarget?> FindAsync(string code, CancellationToken cancellationToken);

    /// <summary>Forgets anything cached for <paramref name="code"/> (called when a link is disabled).</summary>
    void Invalidate(string code);
}
