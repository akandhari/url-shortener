using UrlShortener.Core.Links;

namespace UrlShortener.UnitTests.Links;

public class ShortCodeTests
{
    [Fact]
    public void Alphabet_is_base58_without_look_alike_characters()
    {
        Assert.Equal(58, ShortCode.Alphabet.Length);
        Assert.Equal(58, ShortCode.Alphabet.Distinct().Count());
        Assert.DoesNotContain('0', ShortCode.Alphabet);
        Assert.DoesNotContain('O', ShortCode.Alphabet);
        Assert.DoesNotContain('I', ShortCode.Alphabet);
        Assert.DoesNotContain('l', ShortCode.Alphabet);
    }

    [Theory]
    [InlineData("abc1234")]
    [InlineData("ZZZZZZZ")]
    [InlineData("1111111")]
    public void IsWellFormed_accepts_seven_base58_characters(string code) =>
        Assert.True(ShortCode.IsWellFormed(code));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc123")]      // too short
    [InlineData("abc12345")]    // too long
    [InlineData("abc123O")]     // capital O is not Base58
    [InlineData("abc1230")]     // zero is not Base58
    [InlineData("abc-123")]     // punctuation
    [InlineData("../etc1")]     // path characters
    public void IsWellFormed_rejects_anything_else(string? code) =>
        Assert.False(ShortCode.IsWellFormed(code));
}
