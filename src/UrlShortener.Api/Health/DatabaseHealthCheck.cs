using Microsoft.Extensions.Diagnostics.HealthChecks;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Api.Health;

/// <summary>Readiness: the API can serve traffic only if it can reach its database.</summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the database.");
}
