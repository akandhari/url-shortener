using UrlShortener.Core.Links;

namespace UrlShortener.UnitTests.Links;

public class AliasRulesTests
{
    [Theory]
    [InlineData("fall-sale", "fall-sale")]
    [InlineData("Fall-Sale", "fall-sale")]                 // stored lower case
    [InlineData("  promo2026  ", "promo2026")]             // trimmed
    [InlineData("abcd", "abcd")]                           // minimum length
    [InlineData("bankholiday", "bankholiday")]             // "bank" isn't a blocked part; whole parts only
    [InlineData("loginpage-tips", "loginpage-tips")]       // "loginpage" isn't "login"
    public void Accepts_and_normalises_valid_aliases(string raw, string expected)
    {
        var result = AliasRules.Validate(raw);

        Assert.True(result.IsValid, result.Error);
        Assert.Equal(expected, result.Alias);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("abc", "4 to 32")]
    [InlineData("a23456789-123456789-123456789-123", "4 to 32")]   // 33 characters
    [InlineData("-sale", "start and end")]
    [InlineData("sale-", "start and end")]
    [InlineData("fall--sale", "single hyphens")]
    [InlineData("fall_sale", "letters, digits")]
    [InlineData("fall sale", "letters, digits")]
    [InlineData("fall.sale", "letters, digits")]
    [InlineData("café", "letters, digits")]
    [InlineData("health", "reserved")]
    [InlineData("scalar", "reserved")]
    [InlineData("secure-login", "impersonate")]
    [InlineData("verify-account", "impersonate")]
    [InlineData("reset-password", "impersonate")]
    public void Rejects_invalid_aliases_with_a_reason(string raw, string reason)
    {
        var result = AliasRules.Validate(raw);

        Assert.False(result.IsValid);
        Assert.Contains(reason, result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Configured_words_are_blocked_too()
    {
        var policy = new AliasPolicy(["Acme"]);

        Assert.False(AliasRules.Validate("acme-deals", policy).IsValid);
        Assert.True(AliasRules.Validate("acme-deals").IsValid);   // not blocked without the configured word
    }
}
