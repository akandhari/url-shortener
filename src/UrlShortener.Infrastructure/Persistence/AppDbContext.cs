using Microsoft.EntityFrameworkCore;
using UrlShortener.Core.Links;

namespace UrlShortener.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ShortLink> Links => Set<ShortLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
