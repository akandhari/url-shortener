using System.Net;
using UrlShortener.Api.StaticPage;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Api;

public sealed class StaticPageTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly HttpClient _client = factory.CreateClient();

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Root_serves_the_page_with_the_content_security_policy()
    {
        using var response = await _client.GetAsync(new Uri("/", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("shorten-form", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(ContentSecurityPolicy.Value, Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
    }

    // Guards the middleware order: "/app.js" also matches the one-segment "/{code}" route, so if static files
    // stop being served before routing, these requests hit the redirect endpoint and return 404.
    [Theory]
    [InlineData("/app.js", "text/javascript")]
    [InlineData("/app.css", "text/css")]
    public async Task Page_assets_are_served_as_files_not_as_short_codes(string path, string contentType)
    {
        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Page_has_no_inline_script_style_or_event_handlers()
    {
        var html = await _client.GetStringAsync(new Uri("/", UriKind.Relative));

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"\son[a-z]+\s*=", html);   // onclick=, onerror=, ...
    }

    [Fact]
    public async Task Script_never_writes_html_from_data()
    {
        var script = await _client.GetStringAsync(new Uri("/app.js", UriKind.Relative));

        Assert.DoesNotContain(".innerHTML", script, StringComparison.Ordinal);
        Assert.DoesNotContain(".outerHTML", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Api_documentation_page_is_not_given_the_strict_policy()
    {
        using var response = await _client.GetAsync(new Uri("/scalar", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }
}
