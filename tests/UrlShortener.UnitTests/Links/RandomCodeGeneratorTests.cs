using UrlShortener.Core.Links;

namespace UrlShortener.UnitTests.Links;

public class RandomCodeGeneratorTests
{
    private readonly RandomCodeGenerator _generator = new();

    [Fact]
    public void Generate_returns_well_formed_codes()
    {
        for (var i = 0; i < 1_000; i++)
        {
            var code = _generator.Generate();
            Assert.True(ShortCode.IsWellFormed(code), $"'{code}' is not a well-formed code");
        }
    }

    [Fact]
    public void Generate_does_not_repeat_in_ten_thousand_codes()
    {
        // With ~2.2 trillion possible codes, a repeat in 10,000 has a probability of about 0.002%.
        var codes = Enumerable.Range(0, 10_000).Select(_ => _generator.Generate()).ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }
}
