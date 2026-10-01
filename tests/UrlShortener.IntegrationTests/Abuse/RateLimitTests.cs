using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Abuse;

public sealed class RateLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient ClientWithLimits(int create, int redirect, int lookup) =>
        factory.WithWebHostBuilder(b => b
                .UseSetting("RateLimits:Create:PermitLimit", create.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .UseSetting("RateLimits:Redirect:PermitLimit", redirect.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .UseSetting("RateLimits:Lookup:PermitLimit", lookup.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Creating_beyond_the_limit_returns_429_with_retry_after_and_problem_details()
    {
        using var client = ClientWithLimits(create: 3, redirect: 1000, lookup: 1000);

        for (var i = 0; i < 3; i++)
        {
            using var ok = await client.PostAsJsonAsync("/api/links", new { url = "https://example.com" });
            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        }

        using var limited = await client.PostAsJsonAsync("/api/links", new { url = "https://example.com" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero, "Retry-After should say how long to wait");
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        var body = await limited.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(429, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Create_limit_does_not_block_redirects_or_lookups()
    {
        using var client = ClientWithLimits(create: 1, redirect: 1000, lookup: 1000);
        var code = await LinkApi.CreateLinkAsync(client, "https://example.com");
        using (var limited = await client.PostAsJsonAsync("/api/links", new { url = "https://example.com" }))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        }

        using var redirect = await client.GetAsync(new Uri($"/{code}", UriKind.Relative));
        using var details = await client.GetAsync(new Uri($"/api/links/{code}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
    }

    [Theory]
    [InlineData("redirect")]
    [InlineData("lookup")]
    public async Task Redirect_and_lookup_limits_trigger_too(string policy)
    {
        using var client = policy == "redirect"
            ? ClientWithLimits(create: 1000, redirect: 2, lookup: 1000)
            : ClientWithLimits(create: 1000, redirect: 1000, lookup: 2);
        var code = await LinkApi.CreateLinkAsync(client, "https://example.com");
        var path = policy == "redirect" ? $"/{code}" : $"/api/links/{code}/stats";

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[2]);
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses.Take(2));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Page_and_health_checks_are_never_limited(string path)
    {
        using var client = ClientWithLimits(create: 1, redirect: 1, lookup: 1);

        for (var i = 0; i < 5; i++)
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
