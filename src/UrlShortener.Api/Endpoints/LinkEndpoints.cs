using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using UrlShortener.Api.Abuse;
using UrlShortener.Core.Links;

namespace UrlShortener.Api.Endpoints;

public static class LinkEndpoints
{
    public static IEndpointRouteBuilder MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var api = app.MapGroup("/api/links").WithTags("Links");

        api.MapPost("/", CreateAsync)
            .RequireRateLimiting(RateLimiting.CreatePolicy)
            .WithName("CreateLink")
            .WithSummary("Create a short link for a URL.");

        api.MapGet("/{code}", GetAsync)
            .RequireRateLimiting(RateLimiting.LookupPolicy)
            .WithName("GetLink")
            .WithSummary("Get a short link's details.");

        api.MapDelete("/{code}", DisableAsync)
            .RequireRateLimiting(RateLimiting.LookupPolicy)
            .WithName("DisableLink")
            .WithSummary("Disable a link (410 Gone from then on). Requires the X-Admin-Key header.");

        api.MapGet("/{code}/stats", GetStatsAsync)
            .RequireRateLimiting(RateLimiting.LookupPolicy)
            .WithName("GetLinkStats")
            .WithSummary("Clicks per UTC day (last 30 days) and top 5 referring sites.");

        // Literal routes (/api, /health, /openapi, /scalar) take precedence over this catch-all by routing rules.
        app.MapGet("/{code}", RedirectAsync)
            .RequireRateLimiting(RateLimiting.RedirectPolicy)
            .WithName("FollowLink")
            .WithTags("Redirect")
            .WithSummary("Redirect to the target URL (302) and record the click.");

        return app;
    }

    private static async Task<Results<Created<LinkResponse>, ProblemHttpResult>> CreateAsync(
        CreateLinkRequest request,
        LinkService links,
        IOptions<ShortLinkOptions> options,
        TimeProvider time,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var result = await links.CreateAsync(request.Url, request.Alias, request.ExpiresAt, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                CreateLinkError.InvalidUrl => TypedResults.Problem(
                    title: "Invalid URL", detail: result.Message, statusCode: StatusCodes.Status400BadRequest),
                CreateLinkError.InvalidAlias => TypedResults.Problem(
                    title: "Invalid alias", detail: result.Message, statusCode: StatusCodes.Status400BadRequest),
                CreateLinkError.AliasTaken => TypedResults.Problem(
                    title: "Alias taken", detail: result.Message, statusCode: StatusCodes.Status409Conflict),
                CreateLinkError.InvalidExpiry => TypedResults.Problem(
                    title: "Invalid expiry", detail: result.Message, statusCode: StatusCodes.Status400BadRequest),
                _ => TypedResults.Problem(
                    title: "Could not create the link", detail: result.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable),
            };
        }

        var response = LinkResponse.From(result.Link, options.Value.ResolveBaseUrl(httpRequest), time.GetUtcNow());
        return TypedResults.Created($"/api/links/{result.Link.Code}", response);
    }

    private static async Task<Results<Ok<LinkResponse>, ProblemHttpResult>> GetAsync(
        string code,
        LinkService links,
        IOptions<ShortLinkOptions> options,
        TimeProvider time,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        // Gone links still return their details (with status), so owners can see what happened.
        var link = await links.ResolveAsync(code, cancellationToken);
        return link is null
            ? NotFound()
            : TypedResults.Ok(LinkResponse.From(link, options.Value.ResolveBaseUrl(httpRequest), time.GetUtcNow()));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DisableAsync(
        string code,
        LinkService links,
        IOptions<AdminOptions> admin,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        switch (AdminKey.Verify(httpRequest, admin.Value))
        {
            case AdminKey.Check.NotConfigured:
                return TypedResults.Problem(
                    title: "Admin actions are switched off", detail: "No admin key is configured on this server.",
                    statusCode: StatusCodes.Status403Forbidden);
            case AdminKey.Check.Denied:
                return TypedResults.Problem(
                    title: "Admin key required", detail: $"Send a valid {AdminKey.HeaderName} header.",
                    statusCode: StatusCodes.Status401Unauthorized);
        }

        return await links.DisableAsync(code, cancellationToken) ? TypedResults.NoContent() : NotFound();
    }

    private static async Task<Results<Ok<LinkStatsResponse>, ProblemHttpResult>> GetStatsAsync(
        string code,
        LinkService links,
        CancellationToken cancellationToken)
    {
        var stats = await links.GetStatsAsync(code, cancellationToken);
        return stats is null ? NotFound() : TypedResults.Ok(LinkStatsResponse.From(stats));
    }

    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> RedirectAsync(
        string code,
        LinkService links,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        Uri.TryCreate(httpRequest.Headers.Referer.ToString(), UriKind.Absolute, out var referrer);
        var visit = await links.VisitAsync(code, referrer, cancellationToken);

        return visit.Outcome switch
        {
            // 302 (not 301): browsers must not cache the redirect, or repeat clicks would never be counted.
            VisitOutcome.Redirect => TypedResults.Redirect(visit.Target!.TargetUrl.AbsoluteUri, permanent: false),

            // 410, not 404: the link existed and was deliberately ended.
            VisitOutcome.Gone => TypedResults.Problem(
                title: visit.GoneBecause == LinkStatus.Disabled ? "This link has been disabled" : "This link has expired",
                statusCode: StatusCodes.Status410Gone),
            _ => NotFound(),
        };
    }

    private static ProblemHttpResult NotFound() =>
        TypedResults.Problem(title: "Short link not found", statusCode: StatusCodes.Status404NotFound);
}
