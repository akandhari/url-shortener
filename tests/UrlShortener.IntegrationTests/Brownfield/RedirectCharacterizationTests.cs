using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Brownfield;

/// <summary>
/// Characterization tests (BF-02): they pin the v0.1 redirect behaviour before CR-002 changes the code underneath,
/// so the refactor cannot silently change what visitors and API clients see.
/// </summary>
public sealed class RedirectCharacterizationTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly HttpClient _client =
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Redirect_is_a_302_to_the_exact_target_with_no_body_needed()
    {
        var code = await LinkApi.CreateLinkAsync(_client, "https://example.com/path?q=1&r=two#frag");

        using var response = await _client.GetAsync(new Uri($"/{code}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(new Uri("https://example.com/path?q=1&r=two#frag"), response.Headers.Location);
    }

    [Fact]
    public async Task Sequential_redirects_are_each_counted()
    {
        var code = await LinkApi.CreateLinkAsync(_client, "https://example.com");

        for (var i = 0; i < 3; i++)
        {
            using var response = await _client.GetAsync(new Uri($"/{code}", UriKind.Relative));
            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        }

        Assert.Equal(3, await LinkApi.WaitForClickCountAsync(_client, code, expected: 3));
    }

    [Fact]
    public async Task Unknown_and_malformed_codes_are_404_and_count_nothing()
    {
        var code = await LinkApi.CreateLinkAsync(_client, "https://example.com");

        using var unknown = await _client.GetAsync(new Uri("/zzzzzzz", UriKind.Relative));
        using var malformed = await _client.GetAsync(new Uri("/bad-code", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
        Assert.Equal(0, await LinkApi.GetClickCountAsync(_client, code));
    }

    [Fact]
    public async Task New_link_starts_at_zero_clicks()
    {
        var code = await LinkApi.CreateLinkAsync(_client, "https://example.com");

        Assert.Equal(0, await LinkApi.GetClickCountAsync(_client, code));
    }
}
