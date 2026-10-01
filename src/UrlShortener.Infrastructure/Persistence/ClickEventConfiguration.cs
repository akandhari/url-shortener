using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Core.Links;

namespace UrlShortener.Infrastructure.Persistence;

internal sealed class ClickEventConfiguration : IEntityTypeConfiguration<ClickEvent>
{
    public void Configure(EntityTypeBuilder<ClickEvent> builder)
    {
        builder.ToTable("ClickEvents");
        builder.HasKey(c => c.Id);

        builder.HasOne<ShortLink>().WithMany().HasForeignKey(c => c.LinkId).OnDelete(DeleteBehavior.Cascade);

        // Same storage as ShortLink.CreatedAt: UTC ticks, so SQL can group clicks by day.
        builder.Property(c => c.OccurredAt)
            .HasConversion(value => value.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

        builder.Property(c => c.ReferrerHost).HasMaxLength(ClickEvent.MaxReferrerHostLength);

        // Stats always ask "clicks of one link over a time range".
        builder.HasIndex(c => new { c.LinkId, c.OccurredAt });
    }
}
