using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using UrlShortener.Api.Endpoints;
using UrlShortener.Api.Health;
using UrlShortener.Api.StaticPage;
using UrlShortener.Core.Links;
using UrlShortener.Infrastructure;
using UrlShortener.Infrastructure.Clicks;
using UrlShortener.Infrastructure.Redirects;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Links")
    ?? throw new InvalidOperationException("Connection string 'Links' is not configured.");

// Core services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICodeGenerator, RandomCodeGenerator>();
builder.Services.AddScoped<LinkService>();

// Abuse rules for target URLs (AB-02): never link to ourselves; optional domain denylist from configuration.
builder.Services.AddSingleton(_ =>
{
    var ownHost = builder.Configuration.GetValue<Uri?>($"{ShortLinkOptions.SectionName}:PublicBaseUrl")?.IdnHost;
    var blocked = builder.Configuration.GetSection("Abuse:BlockedDomains").Get<string[]>() ?? [];
    return new TargetUrlPolicy(ownHost is null ? [] : [ownHost], blocked);
});

// Persistence
builder.Services.AddInfrastructure(connectionString);
builder.Services.Configure<ClickRecordingOptions>(builder.Configuration.GetSection(ClickRecordingOptions.SectionName));
builder.Services.Configure<RedirectCacheOptions>(builder.Configuration.GetSection(RedirectCacheOptions.SectionName));

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

// Our web page (wwwroot). Files are served BEFORE routing: "/app.js" also matches the one-segment "/{code}" route,
// and the static file middleware steps aside once an endpoint is chosen, so after routing it would return 404.
app.UseDefaultFiles();   // "/" -> "/index.html"
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
        context.Context.Response.Headers.ContentSecurityPolicy = ContentSecurityPolicy.Value,
});
app.UseRouting();

// API description. Open to everyone in this prototype so reviewers can try it; restrict it in production.
app.MapOpenApi();
app.MapScalarApiReference();

// Liveness: the process is up. Runs no checks, so a database outage doesn't get the process restarted.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: the process can serve traffic (database reachable).
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapLinkEndpoints();

await app.RunAsync();
