using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrlShortener.Core.Links;
using UrlShortener.Infrastructure.Clicks;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Brownfield;

public sealed class ClickRecordingTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly HttpClient _client =
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Each_redirect_stores_a_click_event_with_the_referrer_host_only()
    {
        var code = await LinkApi.CreateLinkAsync(_client, "https://example.com");

        using (var fromNews = new HttpRequestMessage(HttpMethod.Get, $"/{code}"))
        {
            fromNews.Headers.Referrer = new Uri("https://News.Example.com/story?session=abc123");
            using var _ = await _client.SendAsync(fromNews);
        }

        using (await _client.GetAsync(new Uri($"/{code}", UriKind.Relative)))
        {
            // direct visit: no Referer header
        }

        Assert.Equal(2, await LinkApi.WaitForClickCountAsync(_client, code, expected: 2));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var linkId = (await db.Links.SingleAsync(l => l.Code == code)).Id;
        var hosts = await db.ClickEvents.Where(c => c.LinkId == linkId).Select(c => c.ReferrerHost).ToListAsync();

        Assert.Equal(2, hosts.Count);
        Assert.Contains("news.example.com", hosts);   // host only: no path, no session token
        Assert.Contains(null, hosts);                 // the direct visit
    }

    [Fact]
    public async Task Click_count_matches_the_number_of_stored_events()
    {
        var code = await LinkApi.CreateLinkAsync(_client, "https://example.com");

        await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            using var response = await _client.GetAsync(new Uri($"/{code}", UriKind.Relative));
        }));

        Assert.Equal(20, await LinkApi.WaitForClickCountAsync(_client, code, expected: 20));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var linkId = (await db.Links.SingleAsync(l => l.Code == code)).Id;
        Assert.Equal(20, await db.ClickEvents.CountAsync(c => c.LinkId == linkId));
    }

    [Fact]
    public void Full_buffer_drops_the_click_and_counts_it_without_throwing()
    {
        var buffer = new ClickBuffer(Options.Create(new ClickRecordingOptions { QueueCapacity = 2 }));

        Assert.True(buffer.TryEnqueue(new ClickEvent(1, DateTimeOffset.UnixEpoch, null)));
        Assert.True(buffer.TryEnqueue(new ClickEvent(1, DateTimeOffset.UnixEpoch, null)));
        Assert.False(buffer.TryEnqueue(new ClickEvent(1, DateTimeOffset.UnixEpoch, null)));

        Assert.Equal(1, buffer.DroppedCount);
    }

    [Fact]
    public void Dropped_clicks_are_logged_once_then_once_per_thousand_not_once_each()
    {
        var buffer = new ClickBuffer(Options.Create(new ClickRecordingOptions { QueueCapacity = 1 }));
        var log = new CapturingLogger<QueuedClickRecorder>();
        var recorder = new QueuedClickRecorder(buffer, log);

        for (var i = 0; i < 2_501; i++)   // 1 fits, 2,500 are dropped
        {
            recorder.Record(new ClickEvent(1, DateTimeOffset.UnixEpoch, null));
        }

        Assert.Equal(2_500, buffer.DroppedCount);
        Assert.Equal(3, log.Entries.Count(e => e.Level == LogLevel.Warning));   // drop 1, 1,000 and 2,000
    }

    [Fact]
    public async Task Stopping_the_writer_flushes_queued_clicks()
    {
        using var database = new SqliteTestDatabase();
        long linkId;
        using (var db = database.CreateContext())
        {
            var link = new ShortLink("abc1234", new Uri("https://example.com"), DateTimeOffset.UnixEpoch);
            db.Links.Add(link);
            await db.SaveChangesAsync();
            linkId = link.Id;
        }

        var services = new ServiceCollection();
        services.AddScoped(_ => database.CreateContext());
        await using var provider = services.BuildServiceProvider();
        var options = Options.Create(new ClickRecordingOptions());
        var buffer = new ClickBuffer(options);
        var log = new CapturingLogger<ClickWriterService>();
        var writer = new ClickWriterService(buffer, provider.GetRequiredService<IServiceScopeFactory>(), options, log);

        // Queue clicks while the writer is not running, then start and immediately stop it (as on shutdown).
        for (var i = 0; i < 5; i++)
        {
            buffer.TryEnqueue(new ClickEvent(linkId, DateTimeOffset.UnixEpoch, null));
        }

        await ((IHostedService)writer).StartAsync(CancellationToken.None);
        await ((IHostedService)writer).StopAsync(CancellationToken.None);

        Assert.DoesNotContain(log.Entries, e => e.Level >= LogLevel.Warning);
        using var check = database.CreateContext();
        Assert.Equal(5, await check.ClickEvents.CountAsync());
        Assert.Equal(5, (await check.Links.SingleAsync()).ClickCount);
    }
}
