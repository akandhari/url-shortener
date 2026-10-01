using System.Globalization;
using System.Threading.RateLimiting;

namespace UrlShortener.Api.Abuse;

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimits";

    public LimitSettings Create { get; set; } = new() { PermitLimit = 10 };

    public LimitSettings Redirect { get; set; } = new() { PermitLimit = 300 };

    public LimitSettings Lookup { get; set; } = new() { PermitLimit = 60 };
}

public sealed class LimitSettings
{
    /// <summary>Requests allowed per client within <see cref="Window"/> (also the burst size).</summary>
    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>Per-client rate limits (AB-03). Policies are attached to endpoints by name.</summary>
public static class RateLimiting
{
    public const string CreatePolicy = "create";
    public const string RedirectPolicy = "redirect";
    public const string LookupPolicy = "lookup";

    public static IServiceCollection AddClientRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>() ?? new();
        Validate(nameof(settings.Create), settings.Create);
        Validate(nameof(settings.Redirect), settings.Redirect);
        Validate(nameof(settings.Lookup), settings.Lookup);

        return services.AddRateLimiter(options =>
        {
            options.AddPolicy(CreatePolicy, context => PerClient(context, settings.Create));
            options.AddPolicy(RedirectPolicy, context => PerClient(context, settings.Redirect));
            options.AddPolicy(LookupPolicy, context => PerClient(context, settings.Lookup));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (rejected, cancellationToken) =>
            {
                var response = rejected.HttpContext.Response;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var problems = rejected.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = rejected.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "Rate limit reached. Try again after the number of seconds in the Retry-After header.",
                    },
                });
            };
        });
    }

    // Fail at startup with a clear message rather than on the first request.
    private static void Validate(string name, LimitSettings limit)
    {
        if (limit.PermitLimit <= 0 || limit.Window <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"{RateLimitSettings.SectionName}:{name} needs PermitLimit > 0 and Window > 0 (got {limit.PermitLimit}, {limit.Window}).");
        }
    }

    // Key = the client's IP address. Behind a proxy this is the proxy's address unless forwarded headers are
    // configured for trusted proxies (a deployment decision, see docs/scenarios/03-ambiguous.md).
    private static RateLimitPartition<string> PerClient(HttpContext context, LimitSettings limit) =>
        RateLimitPartition.GetTokenBucketLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = limit.PermitLimit,
                TokensPerPeriod = limit.PermitLimit,
                ReplenishmentPeriod = limit.Window,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
}
