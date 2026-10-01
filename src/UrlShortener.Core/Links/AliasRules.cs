using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace UrlShortener.Core.Links;

public sealed record AliasValidation(string? Alias, string? Error)
{
    [MemberNotNullWhen(true, nameof(Alias))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsValid => Alias is not null;
}

/// <summary>Configurable impersonation words for aliases (AB-03b), on top of the built-in defaults.</summary>
public sealed class AliasPolicy(IEnumerable<string> extraBlockedWords)
{
    // Must be declared before Default: static fields are initialised in the order they are written.
    private static readonly string[] DefaultBlockedWords =
        ["login", "signin", "password", "verify", "account", "secure", "wallet", "support"];

    public static readonly AliasPolicy Default = new([]);

    public IReadOnlySet<string> BlockedWords { get; } = DefaultBlockedWords
        .Concat(extraBlockedWords.Select(w => w.Trim().ToLowerInvariant()))
        .Where(w => w.Length > 0)
        .ToHashSet(StringComparer.Ordinal);
}

/// <summary>Rules for user-chosen codes ("fall-sale").</summary>
public static partial class AliasRules
{
    public const int MinLength = 4;
    public const int MaxLength = 32;   // the size of the Code column

    // Words that would shadow our routes or look like our own pages.
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "api", "health", "scalar", "openapi", "admin", "static", "assets", "index", "app", "favicon", "robots", "sitemap",
    };

    public static AliasValidation Validate(string? raw, AliasPolicy? policy = null)
    {
        policy ??= AliasPolicy.Default;

        // Stored in lower case, so "Fall-Sale" and "fall-sale" can't both exist and look the same to people.
        var alias = raw?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(alias))
        {
            return Invalid("The alias is empty.");
        }

        if (alias.Length is < MinLength or > MaxLength)
        {
            return Invalid($"An alias must be {MinLength} to {MaxLength} characters long.");
        }

        if (!Format().IsMatch(alias))
        {
            return Invalid("An alias may contain letters, digits and single hyphens, and must start and end with a letter or digit.");
        }

        if (Reserved.Contains(alias))
        {
            return Invalid("This alias is reserved.");
        }

        if (alias.Split('-').Any(policy.BlockedWords.Contains))
        {
            return Invalid("This alias contains a word that could be used to impersonate a sign-in or account page.");
        }

        return new AliasValidation(alias, null);
    }

    /// <summary>True when <paramref name="code"/> has the shape of an alias (used to reject garbage before the database).</summary>
    public static bool HasAliasShape([NotNullWhen(true)] string? code) =>
        code is { Length: >= MinLength and <= MaxLength } && Format().IsMatch(code);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();

    private static AliasValidation Invalid(string error) => new(null, error);
}
