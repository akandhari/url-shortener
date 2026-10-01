using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Core.Links;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<ILinkRepository, EfLinkRepository>();
        return services;
    }

    /// <summary>
    /// Applies pending migrations at startup. Fine for a single-node prototype; in production this would be
    /// a separate deployment step so several instances do not race to migrate.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
