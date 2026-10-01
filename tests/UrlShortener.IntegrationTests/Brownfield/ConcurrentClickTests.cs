using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Brownfield;

/// <summary>
/// BF-03: proves the lost-update bug in v0.1's read-modify-write click count. Every redirect succeeds, but
/// concurrent ones overwrite each other's count. Expected to FAIL until BF-06.
/// </summary>
public sealed class ConcurrentClickTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private const int Visitors = 50;

    private readonly HttpClient _client =
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Concurrent_redirects_are_all_counted()
    {
        var code = await LinkApi.CreateLinkAsync(_client, "https://example.com/popular");

        var redirects = await Task.WhenAll(Enumerable.Range(0, Visitors).Select(async _ =>
        {
            using var response = await _client.GetAsync(new Uri($"/{code}", UriKind.Relative));
            return response.StatusCode;
        }));

        Assert.All(redirects, status => Assert.Equal(HttpStatusCode.Found, status));
        Assert.Equal(Visitors, await LinkApi.WaitForClickCountAsync(_client, code, expected: Visitors));
    }
}
