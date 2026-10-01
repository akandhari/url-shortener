using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Core.Links;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Brownfield;

public sealed class LinkStatsTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly HttpClient _client = factory.CreateClient();

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Stats_return_exact_clicks_per_utc_day_and_top_referrers()
    {
        var code = await LinkApi.CreateLinkAsync(_client, "https://example.com");
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        await SeedClicksAsync(code, totalClicks: 6,
            (today.AddMinutes(1), "news.example.com"),
            (today.AddMinutes(2), "news.example.com"),
            (today.AddMinutes(3), null),
            (yesterday.AddHours(12), "news.example.com"),
            (yesterday.AddHours(13), "other.example.com"),
            (today.AddDays(-40), "old.example.com"));   // outside the 30-day window: in referrers, not per day

        using var response = await _client.GetAsync(new Uri($"/api/links/{code}/stats", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal(6, body.GetProperty("totalClicks").GetInt64());

        var perDay = body.GetProperty("clicksPerDay").EnumerateArray()
            .Select(d => (d.GetProperty("date").GetString(), d.GetProperty("clicks").GetInt32())).ToList();
        Assert.Equal([(yesterday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 2), (today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 3)], perDay);

        var referrers = body.GetProperty("topReferrers").EnumerateArray()
            .Select(r => (r.GetProperty("host").GetString(), r.GetProperty("clicks").GetInt32())).ToList();
        Assert.Equal(
            [("news.example.com", 3), ("(direct)", 1), ("old.example.com", 1), ("other.example.com", 1)],
            referrers);
    }

    [Fact]
    public async Task New_link_has_empty_stats()
    {
        var code = await LinkApi.CreateLinkAsync(_client, "https://example.com");

        var body = await _client.GetFromJsonAsync<JsonElement>(new Uri($"/api/links/{code}/stats", UriKind.Relative));

        Assert.Equal(0, body.GetProperty("totalClicks").GetInt64());
        Assert.Equal(0, body.GetProperty("clicksPerDay").GetArrayLength());
        Assert.Equal(0, body.GetProperty("topReferrers").GetArrayLength());
    }

    [Theory]
    [InlineData("/api/links/zzzzzzz/stats")]
    [InlineData("/api/links/bad-code/stats")]
    public async Task Unknown_codes_return_404(string path)
    {
        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OpenApi_lists_the_stats_endpoint()
    {
        var document = await _client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Contains("\"/api/links/{code}/stats\"", document, StringComparison.Ordinal);
    }

    private async Task SeedClicksAsync(string code, long totalClicks, params (DateTime At, string? Referrer)[] clicks)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var linkId = (await db.Links.SingleAsync(l => l.Code == code)).Id;

        db.ClickEvents.AddRange(clicks.Select(c => new ClickEvent(linkId, new DateTimeOffset(c.At, TimeSpan.Zero), c.Referrer)));
        await db.SaveChangesAsync();
        await db.Links.Where(l => l.Id == linkId).ExecuteUpdateAsync(s => s.SetProperty(l => l.ClickCount, totalClicks));
    }
}
