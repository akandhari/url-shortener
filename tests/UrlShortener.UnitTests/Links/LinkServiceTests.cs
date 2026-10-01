using UrlShortener.Core.Links;

namespace UrlShortener.UnitTests.Links;

public class LinkServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeLinkRepository _repository = new();
    private readonly FixedTimeProvider _time = new(Now);
    private readonly FakeClickRecorder _clicks = new();
    private readonly FakeStatsQuery _stats = new();

    [Fact]
    public async Task CreateAsync_stores_and_returns_a_new_link()
    {
        var service = new LinkService(_repository, new RandomCodeGenerator(), new RepositoryLookup(_repository), _clicks, _stats, _time);

        var result = await service.CreateAsync("https://example.com/page");

        Assert.True(result.IsSuccess);
        Assert.True(ShortCode.IsWellFormed(result.Link.Code));
        Assert.Equal(new Uri("https://example.com/page"), result.Link.TargetUrl);
        Assert.Equal(Now, result.Link.CreatedAt);
        Assert.Equal(0, result.Link.ClickCount);
        Assert.Same(result.Link, Assert.Single(_repository.Links));
    }

    [Fact]
    public async Task CreateAsync_gives_the_same_url_a_new_code_each_time()
    {
        var service = new LinkService(_repository, new RandomCodeGenerator(), new RepositoryLookup(_repository), _clicks, _stats, _time);

        var first = await service.CreateAsync("https://example.com");
        var second = await service.CreateAsync("https://example.com");

        Assert.NotEqual(first.Link!.Code, second.Link!.Code);
    }

    [Fact]
    public async Task CreateAsync_rejects_an_invalid_url_and_stores_nothing()
    {
        var service = new LinkService(_repository, new RandomCodeGenerator(), new RepositoryLookup(_repository), _clicks, _stats, _time);

        var result = await service.CreateAsync("javascript:alert(1)");

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateLinkError.InvalidUrl, result.Error);
        Assert.Empty(_repository.Links);
        Assert.Equal(0, _repository.AddAttempts);
    }

    [Fact]
    public async Task CreateAsync_retries_with_a_new_code_when_the_code_is_taken()
    {
        await _repository.TryAddAsync(new ShortLink("aaaaaaa", new Uri("https://taken.example"), Now), default);
        var generator = new SequenceCodeGenerator("aaaaaaa", "bbbbbbb");
        var service = new LinkService(_repository, generator, new RepositoryLookup(_repository), _clicks, _stats, _time);

        var result = await service.CreateAsync("https://example.com");

        Assert.True(result.IsSuccess);
        Assert.Equal("bbbbbbb", result.Link.Code);
    }

    [Fact]
    public async Task CreateAsync_gives_up_after_the_maximum_number_of_collisions()
    {
        await _repository.TryAddAsync(new ShortLink("aaaaaaa", new Uri("https://taken.example"), Now), default);
        var alwaysTaken = new SequenceCodeGenerator(Enumerable.Repeat("aaaaaaa", 10).ToArray());
        var service = new LinkService(_repository, alwaysTaken, new RepositoryLookup(_repository), _clicks, _stats, _time);

        var result = await service.CreateAsync("https://example.com");

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateLinkError.NoUniqueCode, result.Error);
        Assert.Equal(LinkService.MaxCodeAttempts, alwaysTaken.Calls);
    }

    [Fact]
    public async Task ResolveAsync_returns_the_link_for_a_known_code()
    {
        var service = new LinkService(_repository, new RandomCodeGenerator(), new RepositoryLookup(_repository), _clicks, _stats, _time);
        var created = await service.CreateAsync("https://example.com");

        var found = await service.ResolveAsync(created.Link!.Code);

        Assert.Same(created.Link, found);
    }

    [Fact]
    public async Task ResolveAsync_returns_null_for_an_unknown_code()
    {
        var service = new LinkService(_repository, new RandomCodeGenerator(), new RepositoryLookup(_repository), _clicks, _stats, _time);

        Assert.Null(await service.ResolveAsync("zzzzzzz"));
        Assert.Equal(1, _repository.FindCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bad_Code!")]
    [InlineData("abc123O")]
    public async Task ResolveAsync_skips_the_repository_for_malformed_codes(string? code)
    {
        var service = new LinkService(_repository, new RandomCodeGenerator(), new RepositoryLookup(_repository), _clicks, _stats, _time);

        Assert.Null(await service.ResolveAsync(code));
        Assert.Equal(0, _repository.FindCalls);
    }

    [Fact]
    public async Task VisitAsync_records_one_click_with_link_time_and_referrer_host()
    {
        var service = new LinkService(_repository, new RandomCodeGenerator(), new RepositoryLookup(_repository), _clicks, _stats, _time);
        var created = await service.CreateAsync("https://example.com");

        var visited = await service.VisitAsync(created.Link!.Code, new Uri("https://News.Example.com/article?token=secret"));

        Assert.Equal(new RedirectTarget(created.Link.Id, created.Link.TargetUrl), visited);
        var click = Assert.Single(_clicks.Recorded);
        Assert.Equal(created.Link.Id, click.LinkId);
        Assert.Equal(Now, click.OccurredAt);
        Assert.Equal("news.example.com", click.ReferrerHost);   // host only: no path, no token
    }

    [Fact]
    public async Task VisitAsync_returns_null_and_records_nothing_for_an_unknown_code()
    {
        var service = new LinkService(_repository, new RandomCodeGenerator(), new RepositoryLookup(_repository), _clicks, _stats, _time);

        Assert.Null(await service.VisitAsync("zzzzzzz", referrer: null));
        Assert.Empty(_clicks.Recorded);
    }

    private sealed class FakeLinkRepository : ILinkRepository
    {
        public List<ShortLink> Links { get; } = [];

        public int AddAttempts { get; private set; }

        public int FindCalls { get; private set; }

        public Task<bool> TryAddAsync(ShortLink link, CancellationToken cancellationToken)
        {
            AddAttempts++;
            if (Links.Any(l => l.Code == link.Code))
            {
                return Task.FromResult(false);
            }

            Links.Add(link);
            return Task.FromResult(true);
        }

        public Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken)
        {
            FindCalls++;
            return Task.FromResult(Links.SingleOrDefault(l => l.Code == code));
        }
    }

    [Fact]
    public async Task GetStatsAsync_asks_for_the_last_30_utc_days_including_today()
    {
        var service = new LinkService(_repository, new RandomCodeGenerator(), new RepositoryLookup(_repository), _clicks, _stats, _time);
        var created = await service.CreateAsync("https://example.com");

        var stats = await service.GetStatsAsync(created.Link!.Code);

        Assert.NotNull(stats);
        Assert.Same(created.Link, stats.Link);
        Assert.Equal(new DateOnly(2026, 9, 1), _stats.RequestedSince);   // Now is 2026-09-30: 30 days back, inclusive
        Assert.Equal(LinkService.TopReferrerCount, _stats.RequestedTop);
    }

    [Theory]
    [InlineData("zzzzzzz")]   // well-formed but unknown
    [InlineData("bad")]       // malformed
    public async Task GetStatsAsync_returns_null_and_runs_no_query_for_unknown_codes(string code)
    {
        var service = new LinkService(_repository, new RandomCodeGenerator(), new RepositoryLookup(_repository), _clicks, _stats, _time);

        Assert.Null(await service.GetStatsAsync(code));
        Assert.Null(_stats.RequestedSince);
    }

    /// <summary>Uncached lookup straight from the fake repository.</summary>
    private sealed class RepositoryLookup(FakeLinkRepository repository) : IRedirectLookup
    {
        public async Task<RedirectTarget?> FindAsync(string code, CancellationToken cancellationToken) =>
            await repository.FindByCodeAsync(code, cancellationToken) is { } link ? new RedirectTarget(link.Id, link.TargetUrl) : null;
    }

    private sealed class FakeStatsQuery : ILinkStatsQuery
    {
        public DateOnly? RequestedSince { get; private set; }

        public int? RequestedTop { get; private set; }

        public Task<IReadOnlyList<DailyClicks>> GetClicksPerDayAsync(long linkId, DateOnly sinceUtcDay, CancellationToken cancellationToken)
        {
            RequestedSince = sinceUtcDay;
            return Task.FromResult<IReadOnlyList<DailyClicks>>([]);
        }

        public Task<IReadOnlyList<ReferrerClicks>> GetTopReferrersAsync(long linkId, int count, CancellationToken cancellationToken)
        {
            RequestedTop = count;
            return Task.FromResult<IReadOnlyList<ReferrerClicks>>([]);
        }
    }

    private sealed class FakeClickRecorder : IClickRecorder
    {
        public List<ClickEvent> Recorded { get; } = [];

        public void Record(ClickEvent click) => Recorded.Add(click);
    }

    private sealed class SequenceCodeGenerator(params string[] codes) : ICodeGenerator
    {
        public int Calls { get; private set; }

        public string Generate() => codes[Calls++];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
