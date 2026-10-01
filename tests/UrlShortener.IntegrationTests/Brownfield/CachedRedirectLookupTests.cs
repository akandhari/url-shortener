using Microsoft.Extensions.Options;
using UrlShortener.Core.Links;
using UrlShortener.Infrastructure.Redirects;

namespace UrlShortener.IntegrationTests.Brownfield;

public sealed class CachedRedirectLookupTests : IDisposable
{
    private static readonly string[] ThreeCodes = ["aaaaaaa", "bbbbbbb", "ccccccc"];

    private readonly CountingRepository _repository = new();
    private readonly RedirectCache _cache = new(Options.Create(new RedirectCacheOptions()));

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task Second_lookup_of_a_known_code_is_served_from_the_cache()
    {
        _repository.Add(new ShortLink("abc1234", new Uri("https://example.com"), DateTimeOffset.UnixEpoch));
        var lookup = new CachedRedirectLookup(_repository, _cache);

        var first = await lookup.FindAsync("abc1234", default);
        var second = await lookup.FindAsync("abc1234", default);

        Assert.Equal(new Uri("https://example.com"), second!.TargetUrl);
        Assert.Equal(first, second);
        Assert.Equal(1, _repository.FindCalls);
    }

    [Fact]
    public async Task Unknown_codes_are_not_cached_so_a_link_created_later_is_found()
    {
        var lookup = new CachedRedirectLookup(_repository, _cache);

        Assert.Null(await lookup.FindAsync("abc1234", default));
        _repository.Add(new ShortLink("abc1234", new Uri("https://example.com"), DateTimeOffset.UnixEpoch));

        Assert.NotNull(await lookup.FindAsync("abc1234", default));
        Assert.Equal(2, _repository.FindCalls);
    }

    [Fact]
    public async Task Cache_size_is_bounded()
    {
        using var small = new RedirectCache(Options.Create(new RedirectCacheOptions { MaxEntries = 2 }));
        var lookup = new CachedRedirectLookup(_repository, small);
        foreach (var code in ThreeCodes)
        {
            _repository.Add(new ShortLink(code, new Uri("https://example.com"), DateTimeOffset.UnixEpoch));
            await lookup.FindAsync(code, default);
        }

        var cached = ThreeCodes.Count(code => small.TryGet(code, out _));

        Assert.Equal(2, cached);
    }

    private sealed class CountingRepository : ILinkRepository
    {
        private readonly List<ShortLink> _links = [];

        public int FindCalls { get; private set; }

        public void Add(ShortLink link) => _links.Add(link);

        public Task<bool> TryAddAsync(ShortLink link, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken)
        {
            FindCalls++;
            return Task.FromResult(_links.SingleOrDefault(l => l.Code == code));
        }

        public Task<bool> DisableAsync(string code, DateTimeOffset disabledAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
