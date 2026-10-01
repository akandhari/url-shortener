using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Abuse;

public sealed class ExpiryAndDisableTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string AdminKey = "test-admin-key-not-a-real-secret";

    private readonly MovableClock _clock = new(DateTimeOffset.UtcNow);

    private HttpClient Client(string? adminKey = AdminKey) =>
        factory.WithWebHostBuilder(b =>
            {
                if (adminKey is not null)
                {
                    b.UseSetting("Admin:ApiKey", adminKey);
                }

                b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(_clock)));
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData(-60)]
    [InlineData(400 * 24 * 60)]
    public async Task Expiry_in_the_past_or_too_far_ahead_is_rejected(int minutesFromNow)
    {
        using var client = Client();

        using var response = await client.PostAsJsonAsync("/api/links",
            new { url = "https://example.com", expiresAt = _clock.Now.AddMinutes(minutesFromNow) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Invalid expiry", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Expired_link_returns_410_even_after_being_cached_and_keeps_its_stats()
    {
        using var client = Client();
        using var created = await client.PostAsJsonAsync("/api/links",
            new { url = "https://example.com/promo", expiresAt = _clock.Now.AddHours(1) });
        var code = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

        using (var live = await client.GetAsync(new Uri($"/{code}", UriKind.Relative)))   // also caches the target
        {
            Assert.Equal(HttpStatusCode.Found, live.StatusCode);
        }

        _clock.Now = _clock.Now.AddHours(2);
        using var gone = await client.GetAsync(new Uri($"/{code}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Gone, gone.StatusCode);
        Assert.Equal("This link has expired", (await gone.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        var details = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/links/{code}", UriKind.Relative));
        Assert.Equal("expired", details.GetProperty("status").GetString());
        Assert.Equal(1, await LinkApi.WaitForClickCountAsync(client, code, expected: 1));   // only the live visit
        using var stats = await client.GetAsync(new Uri($"/api/links/{code}/stats", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
    }

    [Fact]
    public async Task Disabling_with_the_admin_key_makes_a_cached_link_410()
    {
        using var client = Client();
        var code = await LinkApi.CreateLinkAsync(client, "https://example.com/phish-report");
        using (var live = await client.GetAsync(new Uri($"/{code}", UriKind.Relative)))   // cached now
        {
            Assert.Equal(HttpStatusCode.Found, live.StatusCode);
        }

        using var disable = new HttpRequestMessage(HttpMethod.Delete, $"/api/links/{code}");
        disable.Headers.Add("X-Admin-Key", AdminKey);
        using var disabled = await client.SendAsync(disable);
        using var gone = await client.GetAsync(new Uri($"/{code}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, gone.StatusCode);
        Assert.Equal("This link has been disabled", (await gone.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        var details = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/links/{code}", UriKind.Relative));
        Assert.Equal("disabled", details.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, details.GetProperty("disabledAt").ValueKind);
    }

    [Theory]
    [InlineData("wrong-key", HttpStatusCode.Unauthorized)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task Disabling_without_the_right_key_is_refused(string? sentKey, HttpStatusCode expected)
    {
        using var client = Client();
        var code = await LinkApi.CreateLinkAsync(client, "https://example.com");

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/links/{code}");
        if (sentKey is not null)
        {
            request.Headers.Add("X-Admin-Key", sentKey);
        }

        using var response = await client.SendAsync(request);
        using var stillLive = await client.GetAsync(new Uri($"/{code}", UriKind.Relative));

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(HttpStatusCode.Found, stillLive.StatusCode);
    }

    [Fact]
    public async Task Disabling_is_switched_off_when_no_admin_key_is_configured()
    {
        using var client = Client(adminKey: null);
        var code = await LinkApi.CreateLinkAsync(client, "https://example.com");

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/links/{code}");
        request.Headers.Add("X-Admin-Key", "anything");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Disabling_an_unknown_code_is_404()
    {
        using var client = Client();
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/links/zzzzzzz");
        request.Headers.Add("X-Admin-Key", AdminKey);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
