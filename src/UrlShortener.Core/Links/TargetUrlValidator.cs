using System.Diagnostics.CodeAnalysis;

namespace UrlShortener.Core.Links;

public sealed record TargetUrlValidation(Uri? Url, string? Error)
{
    [MemberNotNullWhen(true, nameof(Url))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsValid => Url is not null;
}

/// <summary>
/// Basic target URL rules (FR-6). Hardened rules against abuse (private addresses, credentials in
/// the URL, redirect loops) are added in AB-02.
/// </summary>
public static class TargetUrlValidator
{
    public const int MaxLength = 2048;

    public static TargetUrlValidation Validate(string? rawTarget)
    {
        var candidate = rawTarget?.Trim();

        if (string.IsNullOrEmpty(candidate))
        {
            return Invalid("A URL is required.");
        }

        if (candidate.Length > MaxLength)
        {
            return Invalid($"The URL must be at most {MaxLength} characters.");
        }

        // On Linux, "/some/path" parses as an absolute file:// URI, so the scheme check below matters too.
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var url))
        {
            return Invalid("The URL must be absolute, for example https://example.com/page.");
        }

        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            return Invalid("Only http and https URLs are allowed.");
        }

        if (string.IsNullOrEmpty(url.Host))
        {
            return Invalid("The URL must include a host name.");
        }

        return new TargetUrlValidation(url, null);
    }

    private static TargetUrlValidation Invalid(string error) => new(null, error);
}
