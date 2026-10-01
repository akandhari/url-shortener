namespace UrlShortener.Core.Links;

/// <summary>Deployment-specific validation settings: our own host names and an optional domain denylist.</summary>
public sealed class TargetUrlPolicy
{
    public static readonly TargetUrlPolicy None = new([], []);

    public TargetUrlPolicy(IEnumerable<string> ownHosts, IEnumerable<string> blockedDomains)
    {
        OwnHosts = Normalise(ownHosts);
        BlockedDomains = Normalise(blockedDomains);
    }

    /// <summary>Hosts this service runs on; links to them would loop through the shortener.</summary>
    public IReadOnlySet<string> OwnHosts { get; }

    /// <summary>Domains (and their subdomains) that may not be linked to.</summary>
    public IReadOnlySet<string> BlockedDomains { get; }

    public bool IsOwnHost(string host) => OwnHosts.Contains(host);

    public bool IsBlocked(string host) =>
        BlockedDomains.Any(domain => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal));

    private static HashSet<string> Normalise(IEnumerable<string> hosts) =>
        hosts.Select(h => h.Trim().TrimEnd('.'))
            .Where(h => h.Length > 0)
            .Select(h => new Uri("http://" + h).IdnHost)   // same normal form as Uri.IdnHost: lower case, punycode
            .ToHashSet(StringComparer.Ordinal);
}
