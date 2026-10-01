using System.Security.Cryptography;
using System.Text;

namespace UrlShortener.Api.Abuse;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>
    /// Shared secret for admin actions (disabling links). Set it through an environment variable (Admin__ApiKey) or
    /// user secrets, never in a file in the repo. When empty, admin actions are switched off.
    /// </summary>
    public string? ApiKey { get; set; }
}

/// <summary>Checks the X-Admin-Key header. A stopgap until real authentication (ADR-0004).</summary>
public static class AdminKey
{
    public const string HeaderName = "X-Admin-Key";

    public enum Check
    {
        Allowed,
        NotConfigured,
        Denied,
    }

    public static Check Verify(HttpRequest request, AdminOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(options.ApiKey))
        {
            return Check.NotConfigured;
        }

        var supplied = request.Headers[HeaderName].ToString();

        // Constant-time comparison, so response timing doesn't reveal how much of the key was right.
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(options.ApiKey))
            ? Check.Allowed
            : Check.Denied;
    }
}
