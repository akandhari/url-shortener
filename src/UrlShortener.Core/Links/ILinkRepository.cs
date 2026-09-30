namespace UrlShortener.Core.Links;

public interface ILinkRepository
{
    /// <summary>
    /// Stores a new link. Returns false when the code is already taken, so the caller can retry
    /// with a new code. Uniqueness is guaranteed by the store, not by a check-then-insert.
    /// </summary>
    Task<bool> TryAddAsync(ShortLink link, CancellationToken cancellationToken);

    Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken);
}
