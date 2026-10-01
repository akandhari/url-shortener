namespace UrlShortener.Api.Endpoints;

public sealed class ShortLinkOptions
{
    public const string SectionName = "ShortLinks";

    /// <summary>
    /// Base address used in the short URLs we hand out, e.g. https://sho.rt/. When not set (local development),
    /// the request's own scheme and host are used. Configure it in any shared environment: the Host header comes
    /// from the caller, so relying on it lets a caller put any host into our links.
    /// </summary>
    public Uri? PublicBaseUrl { get; set; }

    public Uri ResolveBaseUrl(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseUrl = PublicBaseUrl ?? new Uri($"{request.Scheme}://{request.Host}");

        // A trailing slash makes new Uri(base, code) append the code instead of replacing the last segment.
        return baseUrl.AbsoluteUri.EndsWith('/') ? baseUrl : new Uri(baseUrl.AbsoluteUri + "/");
    }
}
