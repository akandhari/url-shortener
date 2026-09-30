using UrlShortener.Core.Links;

namespace UrlShortener.UnitTests.Links;

public class TargetUrlValidatorTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/path/page?query=1&other=two#section")]
    [InlineData("https://sub.example.co.uk:8443/a/b")]
    [InlineData("https://münchen.de/straße")]              // internationalised domain and path
    [InlineData("  https://example.com/trimmed  ")]         // surrounding whitespace is trimmed
    public void Accepts_absolute_http_and_https_urls(string input)
    {
        var result = TargetUrlValidator.Validate(input);

        Assert.True(result.IsValid, result.Error);
        Assert.True(result.Url.Scheme is "http" or "https");
    }

    [Theory]
    [InlineData(null, "required")]
    [InlineData("", "required")]
    [InlineData("   ", "required")]
    [InlineData("example.com", "absolute")]
    [InlineData("www.example.com/page", "absolute")]
    [InlineData("https://", "absolute")]
    [InlineData("/relative/path", "")]                      // absolute file:// on Linux, relative on Windows
    [InlineData("ftp://example.com/file", "http and https")]
    [InlineData("javascript:alert(1)", "http and https")]
    [InlineData("mailto:someone@example.com", "http and https")]
    [InlineData("file:///etc/passwd", "http and https")]
    [InlineData("data:text/html,<script>alert(1)</script>", "http and https")]
    public void Rejects_invalid_urls_with_a_reason(string? input, string expectedReason)
    {
        var result = TargetUrlValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Contains(expectedReason, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_urls_longer_than_the_limit()
    {
        var prefix = "https://example.com/";
        var atLimit = prefix + new string('a', TargetUrlValidator.MaxLength - prefix.Length);
        var overLimit = atLimit + "a";

        Assert.True(TargetUrlValidator.Validate(atLimit).IsValid);
        Assert.Contains("at most", TargetUrlValidator.Validate(overLimit).Error, StringComparison.Ordinal);
    }
}
