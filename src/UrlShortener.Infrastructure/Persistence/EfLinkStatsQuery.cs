using Microsoft.EntityFrameworkCore;
using UrlShortener.Core.Links;

namespace UrlShortener.Infrastructure.Persistence;

internal sealed class EfLinkStatsQuery(AppDbContext db) : ILinkStatsQuery
{
    public async Task<IReadOnlyList<DailyClicks>> GetClicksPerDayAsync(
        long linkId, DateOnly sinceUtcDay, CancellationToken cancellationToken)
    {
        var sinceTicks = sinceUtcDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Ticks;

        // OccurredAt is stored as UTC ticks, so a UTC day is ticks / TicksPerDay. EF Core can't do arithmetic on a
        // value-converted property, so this query is SQL. Interpolated values are sent as parameters, not concatenated.
        var rows = await db.Database
            .SqlQuery<DayRow>($"""
                SELECT OccurredAt / {TimeSpan.TicksPerDay} AS Day, COUNT(*) AS Clicks
                FROM ClickEvents
                WHERE LinkId = {linkId} AND OccurredAt >= {sinceTicks}
                GROUP BY Day
                ORDER BY Day
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new DailyClicks(DateOnly.FromDateTime(new DateTime(r.Day * TimeSpan.TicksPerDay, DateTimeKind.Utc)), r.Clicks))
            .ToList();
    }

    public async Task<IReadOnlyList<ReferrerClicks>> GetTopReferrersAsync(
        long linkId, int count, CancellationToken cancellationToken)
    {
        // Sort on an anonymous projection: EF Core can't translate ordering by members of a record built in the query.
        var rows = await db.ClickEvents
            .Where(c => c.LinkId == linkId)
            .GroupBy(c => c.ReferrerHost)
            .Select(g => new { Host = g.Key, Clicks = g.Count() })
            .OrderByDescending(r => r.Clicks)
            .ThenBy(r => r.Host)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => new ReferrerClicks(r.Host, r.Clicks)).ToList();
    }

    private sealed class DayRow
    {
        public long Day { get; init; }

        public int Clicks { get; init; }
    }
}
