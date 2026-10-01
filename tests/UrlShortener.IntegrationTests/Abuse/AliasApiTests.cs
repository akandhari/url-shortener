using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Abuse;

public sealed class AliasApiTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly HttpClient _client =
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Alias_becomes_the_code_and_redirect_details_and_stats_work()
    {
        using var created = await _client.PostAsJsonAsync("/api/links", new { url = "https://example.com/sale", alias = "Fall-Sale-2026" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("fall-sale-2026", body.GetProperty("code").GetString());
        Assert.EndsWith("/fall-sale-2026", body.GetProperty("shortUrl").GetString(), StringComparison.Ordinal);

        using var redirect = await _client.GetAsync(new Uri("/fall-sale-2026", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
        Assert.Equal(new Uri("https://example.com/sale"), redirect.Headers.Location);
        Assert.Equal(1, await LinkApi.WaitForClickCountAsync(_client, "fall-sale-2026", expected: 1));

        using var stats = await _client.GetAsync(new Uri("/api/links/fall-sale-2026/stats", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
    }

    [Fact]
    public async Task Taken_alias_returns_409_and_keeps_the_original()
    {
        using var first = await _client.PostAsJsonAsync("/api/links", new { url = "https://first.example", alias = "team-offsite" });
        using var second = await _client.PostAsJsonAsync("/api/links", new { url = "https://second.example", alias = "TEAM-OFFSITE" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        using var redirect = await _client.GetAsync(new Uri("/team-offsite", UriKind.Relative));
        Assert.Equal(new Uri("https://first.example"), redirect.Headers.Location);
    }

    [Theory]
    [InlineData("admin", "reserved")]
    [InlineData("secure-login", "impersonate")]
    [InlineData("no", "4 to 32")]
    public async Task Invalid_aliases_return_400_with_the_reason(string alias, string reason)
    {
        using var response = await _client.PostAsJsonAsync("/api/links", new { url = "https://example.com", alias });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid alias", body.GetProperty("title").GetString());
        Assert.Contains(reason, body.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Without_an_alias_a_random_code_is_generated_as_before()
    {
        var code = await LinkApi.CreateLinkAsync(_client, "https://example.com");

        Assert.True(UrlShortener.Core.Links.ShortCode.IsGenerated(code));
    }
}
