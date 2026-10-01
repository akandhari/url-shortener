using System.Net.Http.Json;
using System.Text.Json;

namespace UrlShortener.IntegrationTests.Support;

/// <summary>Small helpers for tests that drive the API over HTTP.</summary>
internal static class LinkApi
{
    public static async Task<string> CreateLinkAsync(HttpClient client, string url)
    {
        using var response = await client.PostAsJsonAsync("/api/links", new { url });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("code").GetString()!;
    }

    public static async Task<long> GetClickCountAsync(HttpClient client, string code)
    {
        var body = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/links/{code}", UriKind.Relative));
        return body.GetProperty("clickCount").GetInt64();
    }

    /// <summary>
    /// Polls until the click count reaches <paramref name="expected"/> or the timeout passes, and returns the last
    /// value seen. Asserts on the outcome, not the timing, so it holds whether counting is immediate or eventual.
    /// </summary>
    public static async Task<long> WaitForClickCountAsync(
        HttpClient client, string code, long expected, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        long count;
        while ((count = await GetClickCountAsync(client, code)) < expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        return count;
    }
}
