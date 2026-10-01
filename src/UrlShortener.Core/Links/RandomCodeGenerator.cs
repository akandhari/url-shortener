using System.Security.Cryptography;

namespace UrlShortener.Core.Links;

/// <summary>
/// Generates codes with a cryptographically secure random source, so codes cannot be predicted
/// from earlier ones. <see cref="RandomNumberGenerator.GetString"/> picks characters without modulo bias.
/// </summary>
public sealed class RandomCodeGenerator : ICodeGenerator
{
    public string Generate() => RandomNumberGenerator.GetString(ShortCode.Alphabet, ShortCode.Length);
}
