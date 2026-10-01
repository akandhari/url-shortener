namespace UrlShortener.Core.Links;

public sealed record DailyClicks(DateOnly Date, int Clicks);

/// <summary>Clicks from one referring site; <see cref="Host"/> is null for direct visits.</summary>
public sealed record ReferrerClicks(string? Host, int Clicks);

public sealed record LinkStats(ShortLink Link, IReadOnlyList<DailyClicks> ClicksPerDay, IReadOnlyList<ReferrerClicks> TopReferrers);

/// <summary>Read model for click analytics (CR-002).</summary>
public interface ILinkStatsQuery
{
    /// <summary>Clicks per UTC day from <paramref name="sinceUtcDay"/> (inclusive), oldest first; only days with clicks.</summary>
    Task<IReadOnlyList<DailyClicks>> GetClicksPerDayAsync(long linkId, DateOnly sinceUtcDay, CancellationToken cancellationToken);

    /// <summary>Referring hosts with the most clicks, all time, most clicks first.</summary>
    Task<IReadOnlyList<ReferrerClicks>> GetTopReferrersAsync(long linkId, int count, CancellationToken cancellationToken);
}
