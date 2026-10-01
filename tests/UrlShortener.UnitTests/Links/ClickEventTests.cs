using UrlShortener.Core.Links;

namespace UrlShortener.UnitTests.Links;

public class ClickEventTests
{
    [Theory]
    [InlineData("https://news.example.com/a/b?token=secret", "news.example.com")]
    [InlineData("http://EXAMPLE.org", "example.org")]
    [InlineData("https://münchen.de/", "xn--mnchen-3ya.de")]   // stored as ASCII (punycode)
    public void ReferrerHostFrom_keeps_only_the_host(string referrer, string expected) =>
        Assert.Equal(expected, ClickEvent.ReferrerHostFrom(new Uri(referrer)));

    [Theory]
    [InlineData("android-app://com.example.app/")]
    [InlineData("file:///C:/page.html")]
    [InlineData("ftp://files.example.com/")]
    public void ReferrerHostFrom_ignores_non_web_referrers(string referrer) =>
        Assert.Null(ClickEvent.ReferrerHostFrom(new Uri(referrer)));

    [Fact]
    public void ReferrerHostFrom_returns_null_for_no_referrer() =>
        Assert.Null(ClickEvent.ReferrerHostFrom(null));
}
