using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using UrlShortener.Core.Links;

namespace UrlShortener.Api.Endpoints;

public static class LinkEndpoints
{
    public static IEndpointRouteBuilder MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var api = app.MapGroup("/api/links").WithTags("Links");

        api.MapPost("/", CreateAsync)
            .WithName("CreateLink")
            .WithSummary("Create a short link for a URL.");

        api.MapGet("/{code}", GetAsync)
            .WithName("GetLink")
            .WithSummary("Get a short link's details.");

        // Literal routes (/api, /health, /openapi, /scalar) take precedence over this catch-all by routing rules.
        app.MapGet("/{code}", RedirectAsync)
            .WithName("FollowLink")
            .WithTags("Redirect")
            .WithSummary("Redirect to the target URL (302) and count the click.");

        return app;
    }

    private static async Task<Results<Created<LinkResponse>, ProblemHttpResult>> CreateAsync(
        CreateLinkRequest request,
        LinkService links,
        IOptions<ShortLinkOptions> options,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var result = await links.CreateAsync(request.Url, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                CreateLinkError.InvalidUrl => TypedResults.Problem(
                    title: "Invalid URL", detail: result.Message, statusCode: StatusCodes.Status400BadRequest),
                _ => TypedResults.Problem(
                    title: "Could not create the link", detail: result.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable),
            };
        }

        var response = LinkResponse.From(result.Link, options.Value.ResolveBaseUrl(httpRequest));
        return TypedResults.Created($"/api/links/{result.Link.Code}", response);
    }

    private static async Task<Results<Ok<LinkResponse>, ProblemHttpResult>> GetAsync(
        string code,
        LinkService links,
        IOptions<ShortLinkOptions> options,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var link = await links.ResolveAsync(code, cancellationToken);
        return link is null
            ? NotFound()
            : TypedResults.Ok(LinkResponse.From(link, options.Value.ResolveBaseUrl(httpRequest)));
    }

    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> RedirectAsync(
        string code,
        LinkService links,
        CancellationToken cancellationToken)
    {
        var link = await links.VisitAsync(code, cancellationToken);

        // 302 (not 301): browsers must not cache the redirect, or repeat clicks would never be counted.
        return link is null ? NotFound() : TypedResults.Redirect(link.TargetUrl.AbsoluteUri, permanent: false);
    }

    private static ProblemHttpResult NotFound() =>
        TypedResults.Problem(title: "Short link not found", statusCode: StatusCodes.Status404NotFound);
}
