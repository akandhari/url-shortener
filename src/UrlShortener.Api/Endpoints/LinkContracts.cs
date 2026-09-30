using UrlShortener.Core.Links;

namespace UrlShortener.Api.Endpoints;

/// <summary>Request body for creating a short link. The URL is a string so invalid input gets our own error message.</summary>
public sealed record CreateLinkRequest(string? Url);

/// <summary>Public representation of a short link.</summary>
public sealed record LinkResponse(string Code, Uri ShortUrl, Uri TargetUrl, DateTimeOffset CreatedAt, long ClickCount)
{
    public static LinkResponse From(ShortLink link, Uri publicBaseUrl) =>
        new(link.Code, new Uri(publicBaseUrl, link.Code), link.TargetUrl, link.CreatedAt, link.ClickCount);
}
