using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace UrlShortener.Core.Links;

public sealed record TargetUrlValidation(Uri? Url, string? Error)
{
    [MemberNotNullWhen(true, nameof(Url))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsValid => Url is not null;
}

/// <summary>
/// Target URL rules (FR-6). Basic shape first, then abuse rules (AB-02): no credentials, no private or local network
/// addresses, no internal host names, no links to this service, optional domain denylist.
/// </summary>
public static class TargetUrlValidator
{
    public const int MaxLength = 2048;

    // Host name suffixes that only exist inside private networks.
    private static readonly string[] InternalSuffixes = [".localhost", ".local", ".internal", ".lan", ".home.arpa"];

    public static TargetUrlValidation Validate(string? rawTarget, TargetUrlPolicy? policy = null)
    {
        policy ??= TargetUrlPolicy.None;

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

        // https://paypal.com@evil.example/ goes to evil.example: the part before @ only deceives the reader.
        if (url.UserInfo.Length > 0)
        {
            return Invalid("URLs with a user name or password are not allowed.");
        }

        // Uri has already normalised every IPv4 notation (decimal, hex, octal, short) to dotted form.
        if (url.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            if (NetworkAddress.IsPrivateOrLocal(IPAddress.Parse(url.Host.Trim('[', ']'))))
            {
                return Invalid("Links to private or local network addresses are not allowed.");
            }
        }
        else if (IsInternalHostName(url.IdnHost))
        {
            return Invalid("Links must point to a public host name.");
        }

        if (policy.IsOwnHost(url.IdnHost))
        {
            return Invalid("Links to this service's own short links are not allowed.");
        }

        if (policy.IsBlocked(url.IdnHost))
        {
            return Invalid("Links to this domain are not allowed.");
        }

        return new TargetUrlValidation(url, null);
    }

    private static bool IsInternalHostName(string host) =>
        host == "localhost"
        || !host.Contains('.', StringComparison.Ordinal)   // single label, e.g. http://intranet/
        || InternalSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.Ordinal));

    private static TargetUrlValidation Invalid(string error) => new(null, error);
}
