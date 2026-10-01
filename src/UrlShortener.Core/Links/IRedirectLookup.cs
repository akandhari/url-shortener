namespace UrlShortener.Core.Links;

/// <summary>What a redirect needs: the link's id (to record the click) and where to send the visitor.</summary>
public sealed record RedirectTarget(long LinkId, Uri TargetUrl);

/// <summary>
/// Finds the redirect target for a code. Separate from <see cref="ILinkRepository"/> so the hot redirect path can be
/// cached without making the details endpoint show a stale click count.
/// </summary>
public interface IRedirectLookup
{
    Task<RedirectTarget?> FindAsync(string code, CancellationToken cancellationToken);
}
