using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using UrlShortener.Api.Endpoints;
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

// HTTP
builder.Services.Configure<ShortLinkOptions>(builder.Configuration.GetSection(ShortLinkOptions.SectionName));
builder.Services.AddProblemDetails();

// A malformed request body is the caller's fault: keep its 400 instead of turning it into a 500.
// (In Development, ASP.NET throws on bad bodies, so without this the status would depend on the environment.)
builder.Services.Configure<ExceptionHandlerOptions>(options =>
    options.StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError);

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

// Unhandled exceptions and empty error responses become ProblemDetails, without stack traces.
app.UseExceptionHandler();
app.UseStatusCodePages();

// API description. Open to everyone in this prototype so reviewers can try it; restrict it in production.
app.MapOpenApi();
app.MapScalarApiReference();

// Liveness: the process is up. Runs no checks, so a database outage doesn't get the process restarted.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: the process can serve traffic (database reachable).
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapLinkEndpoints();

await app.RunAsync();
