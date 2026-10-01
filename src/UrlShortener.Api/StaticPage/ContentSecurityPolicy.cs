namespace UrlShortener.Api.StaticPage;

/// <summary>
/// Content-Security-Policy for our own web page (wwwroot). It tells the browser which sources it may load and run;
/// anything else is blocked, so even an injected script would not execute.
/// Applied only to the static page: the Scalar API page uses inline scripts and styles and would break under it.
/// Site-wide security headers are handled in AB-05.
/// </summary>
public static class ContentSecurityPolicy
{
    public static readonly string Value = string.Join("; ",
        "default-src 'self'",       // by default, load anything only from our own site
        "script-src 'self'",        // JavaScript only from our files (app.js); blocks inline <script> and onerror= handlers
        "style-src 'self'",         // CSS only from our files (app.css); blocks inline styles
        "img-src 'self' data:",     // images from our site, or small built-in data: images
        "connect-src 'self'",       // fetch() may only call our own API, so data can't be sent elsewhere
        "frame-ancestors 'none'",   // no site may embed this page in a frame (clickjacking)
        "base-uri 'none'",          // an injected <base> tag cannot redirect relative links
        "form-action 'self'");      // forms may only submit to our own site
}
