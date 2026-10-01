using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace UrlShortener.Infrastructure.Persistence;

/// <summary>
/// Stores DateTimeOffset as UTC ticks (a number). SQLite has no date-offset type, and EF Core can't sort or compare
/// DateTimeOffset stored as text there. EF applies it to nullable properties too (nulls stay null).
/// </summary>
internal sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.UtcTicks,
    ticks => new DateTimeOffset(ticks, TimeSpan.Zero))
{
    public static readonly UtcTicksConverter Instance = new();
}
