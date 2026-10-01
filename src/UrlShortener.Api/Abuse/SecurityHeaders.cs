namespace UrlShortener.Api.Abuse;

/// <summary>
/// Security headers on every response (AB-05). The web page additionally carries its strict Content-Security-Policy
/// (GF-05); /scalar does not, because it uses inline scripts and styles.
/// </summary>
public static class SecurityHeaders
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";                          // never guess a content type (e.g. JSON as HTML)
            headers.XFrameOptions = "DENY";                                   // no framing (clickjacking), for older browsers too
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";   // targets see our origin only, never a full URL
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            await next(context);
        });
}
