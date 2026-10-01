namespace UrlShortener.Core.Links;

public sealed class LinkService(ILinkRepository repository, ICodeGenerator codeGenerator, TimeProvider timeProvider)
{
    /// <summary>
    /// With ~2.2 trillion possible codes a single collision is rare; five in a row means something is broken
    /// (for example a faulty generator), so we stop instead of looping.
    /// </summary>
    public const int MaxCodeAttempts = 5;

    public async Task<CreateLinkResult> CreateAsync(string? rawTarget, CancellationToken cancellationToken = default)
    {
        var validation = TargetUrlValidator.Validate(rawTarget);
        if (!validation.IsValid)
        {
            return CreateLinkResult.Failure(CreateLinkError.InvalidUrl, validation.Error);
        }

        for (var attempt = 1; attempt <= MaxCodeAttempts; attempt++)
        {
            var link = new ShortLink(codeGenerator.Generate(), validation.Url, timeProvider.GetUtcNow());
            if (await repository.TryAddAsync(link, cancellationToken).ConfigureAwait(false))
            {
                return CreateLinkResult.Success(link);
            }
        }

        return CreateLinkResult.Failure(
            CreateLinkError.NoUniqueCode, "Could not allocate a unique short code. Please try again.");
    }

    /// <summary>Returns the link for <paramref name="code"/>, or null when it does not exist.</summary>
    public Task<ShortLink?> ResolveAsync(string? code, CancellationToken cancellationToken = default)
    {
        // Malformed codes can never exist, so skip the database round trip.
        if (!ShortCode.IsWellFormed(code))
        {
            return Task.FromResult<ShortLink?>(null);
        }

        return repository.FindByCodeAsync(code, cancellationToken);
    }

    /// <summary>
    /// Resolves a code for a redirect and counts the click. Returns null when the code does not exist.
    /// </summary>
    public async Task<ShortLink?> VisitAsync(string? code, CancellationToken cancellationToken = default)
    {
        var link = await ResolveAsync(code, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            return null;
        }

        // v0.1 simplification: read-modify-write on the link row. Correct for one visitor at a time, but
        // concurrent redirects can overwrite each other's count. Revisited in CR-002 (brownfield scenario).
        link.RegisterClick();
        await repository.UpdateAsync(link, cancellationToken).ConfigureAwait(false);
        return link;
    }
}
