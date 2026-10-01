using UrlShortener.Core.Links;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Persistence;

public sealed class EfLinkRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Created = new(2026, 9, 30, 12, 34, 56, TimeSpan.Zero);

    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Saved_link_is_found_by_code_with_the_same_values()
    {
        using (var db = _database.CreateContext())
        {
            var saved = await new EfLinkRepository(db).TryAddAsync(
                new ShortLink("abc1234", new Uri("https://example.com/page?q=1"), Created), default);
            Assert.True(saved);
        }

        // A fresh context proves the values come from the database, not from EF's change tracker.
        using var readDb = _database.CreateContext();
        var found = await new EfLinkRepository(readDb).FindByCodeAsync("abc1234", default);

        Assert.NotNull(found);
        Assert.True(found.Id > 0);
        Assert.Equal(new Uri("https://example.com/page?q=1"), found.TargetUrl);
        Assert.Equal(Created, found.CreatedAt);
        Assert.Equal(0, found.ClickCount);
    }

    [Fact]
    public async Task Taken_code_returns_false_and_the_context_stays_usable()
    {
        using var db = _database.CreateContext();
        var repository = new EfLinkRepository(db);
        await repository.TryAddAsync(new ShortLink("abc1234", new Uri("https://first.example"), Created), default);

        var duplicate = await repository.TryAddAsync(
            new ShortLink("abc1234", new Uri("https://second.example"), Created), default);
        var next = await repository.TryAddAsync(
            new ShortLink("xyz9876", new Uri("https://third.example"), Created), default);

        Assert.False(duplicate);
        Assert.True(next);
        using var readDb = _database.CreateContext();
        Assert.Equal(2, readDb.Links.Count());
        Assert.Equal(new Uri("https://first.example"), readDb.Links.Single(l => l.Code == "abc1234").TargetUrl);
    }

    [Fact]
    public async Task Codes_are_case_sensitive()
    {
        using var db = _database.CreateContext();
        var repository = new EfLinkRepository(db);

        Assert.True(await repository.TryAddAsync(new ShortLink("abcdefg", new Uri("https://a.example"), Created), default));
        Assert.True(await repository.TryAddAsync(new ShortLink("ABCDEFG", new Uri("https://b.example"), Created), default));
        Assert.Equal(new Uri("https://b.example"), (await repository.FindByCodeAsync("ABCDEFG", default))!.TargetUrl);
    }

    [Fact]
    public async Task Unknown_code_returns_null()
    {
        using var db = _database.CreateContext();

        Assert.Null(await new EfLinkRepository(db).FindByCodeAsync("zzzzzzz", default));
    }

    [Fact]
    public async Task LinkService_retries_a_taken_code_against_the_real_database()
    {
        using var db = _database.CreateContext();
        var repository = new EfLinkRepository(db);
        await repository.TryAddAsync(new ShortLink("aaaaaaa", new Uri("https://taken.example"), Created), default);
        var service = new LinkService(
            repository, new SequenceCodeGenerator("aaaaaaa", "bbbbbbb"), new IgnoreClicks(), new EfLinkStatsQuery(db), TimeProvider.System);

        var result = await service.CreateAsync("https://example.com");

        Assert.True(result.IsSuccess);
        Assert.Equal("bbbbbbb", result.Link.Code);
    }

    private sealed class IgnoreClicks : IClickRecorder
    {
        public void Record(ClickEvent click)
        {
        }
    }

    private sealed class SequenceCodeGenerator(params string[] codes) : ICodeGenerator
    {
        private int _next;

        public string Generate() => codes[_next++];
    }
}
