using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Core.Links;

namespace UrlShortener.Infrastructure.Persistence;

/// <summary>
/// Database mapping for <see cref="ShortLink"/>. Kept here, not as attributes on the entity, so Core stays free of EF Core.
/// </summary>
internal sealed class ShortLinkConfiguration : IEntityTypeConfiguration<ShortLink>
{
    public void Configure(EntityTypeBuilder<ShortLink> builder)
    {
        builder.ToTable("Links");
        builder.HasKey(l => l.Id);

        // Room for custom aliases later (AB-03b). SQLite compares text case-sensitively by default,
        // which is what Base58 needs ("abc" and "ABC" are different codes).
        builder.Property(l => l.Code).IsRequired().HasMaxLength(32);

        // Uniqueness is guaranteed here, by the database, not by a check-then-insert that could race.
        builder.HasIndex(l => l.Code).IsUnique();

        builder.Property(l => l.TargetUrl)
            .IsRequired()
            .HasMaxLength(TargetUrlValidator.MaxLength)
            .HasConversion(url => url.AbsoluteUri, text => new Uri(text, UriKind.Absolute));

        // Dates as UTC ticks (see UtcTicksConverter) so ordering and grouping work in SQL.
        builder.Property(l => l.CreatedAt).HasConversion(UtcTicksConverter.Instance);
        builder.Property(l => l.ExpiresAt).HasConversion(UtcTicksConverter.Instance);
        builder.Property(l => l.DisabledAt).HasConversion(UtcTicksConverter.Instance);
    }
}
