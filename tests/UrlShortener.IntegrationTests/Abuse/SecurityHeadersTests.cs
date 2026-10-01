using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Abuse;

public sealed class SecurityHeadersTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("/")]                    // web page
    [InlineData("/app.js")]              // static file
    [InlineData("/health/live")]
    [InlineData("/zzzzzzz")]             // 404 ProblemDetails
    [InlineData("/api/links/zzzzzzz")]
    [InlineData("/openapi/v1.json")]
    public async Task Every_response_carries_the_security_headers(string path)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header(response, "Referrer-Policy"));
        Assert.Equal("camera=(), microphone=(), geolocation=()", Header(response, "Permissions-Policy"));
        Assert.Equal("same-origin", Header(response, "Cross-Origin-Opener-Policy"));
    }

    [Fact]
    public async Task Redirects_carry_them_too()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var code = await LinkApi.CreateLinkAsync(client, "https://example.com");

        using var response = await client.GetAsync(new Uri($"/{code}", UriKind.Relative));

        Assert.Equal(System.Net.HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("strict-origin-when-cross-origin", Header(response, "Referrer-Policy"));
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
