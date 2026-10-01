using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Abuse;

public sealed class BlockedTargetApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("http://2130706433/", "private or local network")]
    [InlineData("https://paypal.com@evil.example/", "user name or password")]
    [InlineData("http://localhost:5173/", "public host name")]
    public async Task Abusive_targets_are_rejected_with_400_and_the_reason(string url, string reason)
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/links", new { url });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(reason, body.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Links_to_our_own_public_host_and_denylisted_domains_are_rejected()
    {
        using var configured = factory.WithWebHostBuilder(b => b
            .UseSetting("ShortLinks:PublicBaseUrl", "https://sho.rt/")
            .UseSetting("Abuse:BlockedDomains:0", "blocked.example"));
        using var client = configured.CreateClient();

        using var loop = await client.PostAsJsonAsync("/api/links", new { url = "https://sho.rt/abc1234" });
        using var denied = await client.PostAsJsonAsync("/api/links", new { url = "https://cdn.blocked.example/x" });
        using var fine = await client.PostAsJsonAsync("/api/links", new { url = "https://example.com/" });

        Assert.Equal(HttpStatusCode.BadRequest, loop.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal(HttpStatusCode.Created, fine.StatusCode);
    }
}
