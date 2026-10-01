using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortener.Core.Links;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Api;

public sealed class LinkEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    // Auto-redirect off, so we can assert on the 302 itself instead of following it.
    private readonly HttpClient _client =
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Create_returns_201_with_location_and_the_link()
    {
        using var response = await _client.PostAsJsonAsync("/api/links", new { url = "https://example.com/page" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var code = body.GetProperty("code").GetString();
        Assert.True(ShortCode.IsWellFormed(code));
        Assert.Equal($"/api/links/{code}", response.Headers.Location?.OriginalString);
        Assert.Equal($"http://localhost/{code}", body.GetProperty("shortUrl").GetString());
        Assert.Equal("https://example.com/page", body.GetProperty("targetUrl").GetString());
        Assert.Equal(0, body.GetProperty("clickCount").GetInt64());
        Assert.True(body.TryGetProperty("createdAt", out _));
    }

    [Theory]
    [InlineData("javascript:alert(1)", "http and https")]
    [InlineData("", "required")]
    [InlineData("/relative/path", "")]
    public async Task Create_rejects_an_invalid_url_with_problem_details(string url, string reason)
    {
        using var response = await _client.PostAsJsonAsync("/api/links", new { url });

        var body = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Invalid URL", body.GetProperty("title").GetString());
        Assert.Contains(reason, body.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    public async Task Create_rejects_a_malformed_body_with_400(string rawBody)
    {
        using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");

        using var response = await _client.PostAsync(new Uri("/api/links", UriKind.Relative), content);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Redirect_returns_302_to_the_target_and_counts_the_click()
    {
        var code = await CreateLinkAsync("https://example.com/target?x=1");

        using var redirect = await _client.GetAsync(new Uri($"/{code}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
        Assert.Equal(new Uri("https://example.com/target?x=1"), redirect.Headers.Location);

        // Since CR-002 (BF-06) clicks are written in the background, so the count is eventually consistent:
        // wait for it instead of reading it immediately.
        Assert.Equal(1, await LinkApi.WaitForClickCountAsync(_client, code, expected: 1));
    }

    [Theory]
    [InlineData("/zzzzzzz")]             // well-formed but unknown
    [InlineData("/not-a-code")]          // malformed
    [InlineData("/api/links/zzzzzzz")]   // details of an unknown code
    public async Task Unknown_codes_return_404_problem_details(string path)
    {
        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative));

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Details_return_the_same_shape_as_create()
    {
        using var created = await _client.PostAsJsonAsync("/api/links", new { url = "https://example.com" });
        var createdBody = await ReadJsonAsync(created);

        using var details = await _client.GetAsync(
            new Uri($"/api/links/{createdBody.GetProperty("code").GetString()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var names = (await ReadJsonAsync(details)).EnumerateObject().Select(p => p.Name);
        Assert.Equal(createdBody.EnumerateObject().Select(p => p.Name), names);
    }

    [Fact]
    public async Task Short_url_uses_the_configured_base_url_not_the_host_header()
    {
        using var configured = factory.WithWebHostBuilder(b => b.UseSetting("ShortLinks:PublicBaseUrl", "https://sho.rt"));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/links")
        {
            Content = JsonContent.Create(new { url = "https://example.com" }),
        };
        request.Headers.Host = "evil.example";

        using var response = await client.SendAsync(request);

        var body = await ReadJsonAsync(response);
        Assert.Equal($"https://sho.rt/{body.GetProperty("code").GetString()}", body.GetProperty("shortUrl").GetString());
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Fixed_routes_are_not_shadowed_by_the_redirect_route(string path)
    {
        using var client = factory.CreateClient();   // follows redirects (Scalar may redirect to a trailing slash)

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_lists_the_link_endpoints()
    {
        var document = await _client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Contains("\"/api/links\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/api/links/{code}\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/{code}\"", document, StringComparison.Ordinal);
    }

    private async Task<string> CreateLinkAsync(string url)
    {
        using var response = await _client.PostAsJsonAsync("/api/links", new { url });
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("code").GetString()!;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();

    // Reads the body once (a response stream can't be read twice) and returns it for further checks.
    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.Equal((int)expected, body.GetProperty("status").GetInt32());
        return body;
    }
}
