namespace UrlShortener.Infrastructure.Redirects;

public sealed class RedirectCacheOptions
{
    public const string SectionName = "RedirectCache";

    /// <summary>How long a code → target entry stays cached.</summary>
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Maximum cached entries, so the cache can't grow without limit.</summary>
    public int MaxEntries { get; set; } = 10_000;
}
