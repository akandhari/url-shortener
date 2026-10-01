using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UrlShortener.Core.Links;

namespace UrlShortener.Infrastructure.Redirects;

/// <summary>
/// Caches code → (link id, target) for the redirect path. A link's target never changes, so a hit is always correct
/// today. Unknown codes are NOT cached: otherwise random requests could fill memory, and a link created right after a
/// miss would keep returning 404. When links can be disabled (AB-04), disabling must remove the entry.
/// </summary>
internal sealed class CachedRedirectLookup(ILinkRepository repository, RedirectCache cache) : IRedirectLookup
{
    public async Task<RedirectTarget?> FindAsync(string code, CancellationToken cancellationToken)
    {
        if (cache.TryGet(code, out var cached))
        {
            return cached;
        }

        var link = await repository.FindByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            return null;
        }

        var target = new RedirectTarget(link.Id, link.TargetUrl);
        cache.Set(code, target);
        return target;
    }
}

/// <summary>Own, size-limited memory cache for redirect targets (not shared with anything else in the app).</summary>
public sealed class RedirectCache(IOptions<RedirectCacheOptions> options) : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = options.Value.MaxEntries });
    private readonly TimeSpan _timeToLive = options.Value.TimeToLive;

    public bool TryGet(string code, out RedirectTarget? target) => _cache.TryGetValue(Key(code), out target);

    public void Set(string code, RedirectTarget target) =>
        _cache.Set(Key(code), target, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = _timeToLive });

    public void Remove(string code) => _cache.Remove(Key(code));

    public void Dispose() => _cache.Dispose();

    private static string Key(string code) => "redirect:" + code;
}
