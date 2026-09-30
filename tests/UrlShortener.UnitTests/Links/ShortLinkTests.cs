using UrlShortener.Core.Links;

namespace UrlShortener.UnitTests.Links;

public class ShortLinkTests
{
    [Fact]
    public void RegisterClick_increments_the_click_count()
    {
        var link = new ShortLink("abc1234", new Uri("https://example.com"), DateTimeOffset.UnixEpoch);

        link.RegisterClick();
        link.RegisterClick();

        Assert.Equal(2, link.ClickCount);
    }
}
