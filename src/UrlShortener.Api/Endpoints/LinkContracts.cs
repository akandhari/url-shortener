using UrlShortener.Core.Links;

namespace UrlShortener.Api.Endpoints;

/// <summary>
/// Request body for creating a short link. Strings, so invalid input gets our own error message.
/// <see cref="Alias"/> is optional (AB-03b).
/// </summary>
public sealed record CreateLinkRequest(string? Url, string? Alias = null);

/// <summary>Public representation of a short link.</summary>
public sealed record LinkResponse(string Code, Uri ShortUrl, Uri TargetUrl, DateTimeOffset CreatedAt, long ClickCount)
{
    public static LinkResponse From(ShortLink link, Uri publicBaseUrl) =>
        new(link.Code, new Uri(publicBaseUrl, link.Code), link.TargetUrl, link.CreatedAt, link.ClickCount);
}

public sealed record DailyClicksResponse(DateOnly Date, int Clicks);

public sealed record ReferrerResponse(string Host, int Clicks);

/// <summary>Click analytics for one link (CR-002).</summary>
public sealed record LinkStatsResponse(
    string Code,
    long TotalClicks,
    IReadOnlyList<DailyClicksResponse> ClicksPerDay,
    IReadOnlyList<ReferrerResponse> TopReferrers)
{
    public const string DirectReferrer = "(direct)";

    public static LinkStatsResponse From(LinkStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return new(
            stats.Link.Code,
            stats.Link.ClickCount,
            stats.ClicksPerDay.Select(d => new DailyClicksResponse(d.Date, d.Clicks)).ToList(),
            stats.TopReferrers.Select(r => new ReferrerResponse(r.Host ?? DirectReferrer, r.Clicks)).ToList());
    }
}
