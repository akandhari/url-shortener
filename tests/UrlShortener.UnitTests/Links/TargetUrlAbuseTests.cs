using UrlShortener.Core.Links;

namespace UrlShortener.UnitTests.Links;

/// <summary>AB-02 attack matrix: each rejected URL must fail for the right reason.</summary>
public class TargetUrlAbuseTests
{
    private const string Credentials = "user name or password";
    private const string PrivateAddress = "private or local network";
    private const string InternalName = "public host name";
    private const string OwnHost = "this service's own";
    private const string Denylisted = "this domain";

    private static readonly TargetUrlPolicy Policy = new(ownHosts: ["sho.rt"], blockedDomains: ["blocked.example"]);

    [Theory]
    // Deceptive credentials
    [InlineData("https://paypal.com@evil.example/", Credentials)]
    [InlineData("https://user:pass@example.com/", Credentials)]
    // Loopback in every notation .NET accepts
    [InlineData("http://127.0.0.1/", PrivateAddress)]
    [InlineData("http://2130706433/", PrivateAddress)]           // decimal
    [InlineData("http://0x7f000001/", PrivateAddress)]           // hex
    [InlineData("http://0177.0.0.1/", PrivateAddress)]           // octal
    [InlineData("http://127.1/", PrivateAddress)]                // short form
    [InlineData("http://[::1]/", PrivateAddress)]
    // Private, link-local, metadata, CGNAT, unspecified, multicast, broadcast
    [InlineData("http://10.0.0.5/", PrivateAddress)]
    [InlineData("http://172.16.0.1/", PrivateAddress)]
    [InlineData("http://172.31.255.255/", PrivateAddress)]
    [InlineData("http://192.168.1.1/admin", PrivateAddress)]
    [InlineData("http://169.254.169.254/latest/meta-data", PrivateAddress)]   // cloud metadata endpoint
    [InlineData("http://100.64.0.1/", PrivateAddress)]
    [InlineData("http://0.0.0.0/", PrivateAddress)]
    [InlineData("http://224.0.0.1/", PrivateAddress)]
    [InlineData("http://255.255.255.255/", PrivateAddress)]
    [InlineData("http://[::ffff:192.168.0.1]/", PrivateAddress)] // IPv4 hidden in IPv6
    [InlineData("http://[fe80::1]/", PrivateAddress)]
    [InlineData("http://[fd00::1]/", PrivateAddress)]            // IPv6 unique local
    // Internal host names
    [InlineData("http://localhost:8080/", InternalName)]
    [InlineData("http://LOCALHOST/", InternalName)]
    [InlineData("http://app.localhost/", InternalName)]
    [InlineData("http://printer.local/", InternalName)]
    [InlineData("http://db.internal/", InternalName)]
    [InlineData("http://intranet/", InternalName)]               // single label
    // Loops through this service, denylisted domains
    [InlineData("https://sho.rt/abc1234", OwnHost)]
    [InlineData("https://SHO.RT/abc1234", OwnHost)]
    [InlineData("https://blocked.example/", Denylisted)]
    [InlineData("https://www.blocked.example/page", Denylisted)]
    public void Rejects_abusive_targets_with_the_right_reason(string url, string reason)
    {
        var result = TargetUrlValidator.Validate(url, Policy);

        Assert.False(result.IsValid, $"{url} should be rejected");
        Assert.Contains(reason, result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("http://93.184.216.34/")]                 // public IPv4 literal
    [InlineData("http://[2606:4700:4700::1111]/")]         // public IPv6 literal
    [InlineData("http://172.32.0.1/")]                     // just outside 172.16.0.0/12
    [InlineData("http://100.128.0.1/")]                    // just outside 100.64.0.0/10
    [InlineData("https://notblocked.example/")]            // suffix match is on whole labels only
    [InlineData("https://sho.rt.example.com/")]            // our host as a prefix is not our host
    [InlineData("https://example.com:8443/path?q=1#x")]
    public void Accepts_public_targets(string url)
    {
        var result = TargetUrlValidator.Validate(url, Policy);

        Assert.True(result.IsValid, result.Error);
    }
}
