using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using UrlShortener.Api.Health;
using UrlShortener.Core.Links;
using UrlShortener.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Links")
    ?? throw new InvalidOperationException("Connection string 'Links' is not configured.");

// Core services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICodeGenerator, RandomCodeGenerator>();
builder.Services.AddScoped<LinkService>();

// Persistence
builder.Services.AddInfrastructure(connectionString);

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

// Liveness: the process is up. Runs no checks, so a database outage doesn't get the process restarted.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: the process can serve traffic (database reachable).
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.RunAsync();
