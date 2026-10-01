namespace UrlShortener.Core.Links;

public sealed class LinkService(
    ILinkRepository repository,
    ICodeGenerator codeGenerator,
    IRedirectLookup redirectLookup,
    IClickRecorder clickRecorder,
    ILinkStatsQuery statsQuery,
    TimeProvider timeProvider,
    TargetUrlPolicy? urlPolicy = null,
    AliasPolicy? aliasPolicy = null)
{
    /// <summary>
    /// With ~2.2 trillion possible codes a single collision is rare; five in a row means something is broken
    /// (for example a faulty generator), so we stop instead of looping.
    /// </summary>
    public const int MaxCodeAttempts = 5;

    /// <summary>Days of per-day history in the stats, including today (UTC).</summary>
    public const int StatsDays = 30;

    /// <summary>How many referring sites the stats list.</summary>
    public const int TopReferrerCount = 5;

    /// <summary>Longest allowed lifetime for a link with an expiry date (AB-04).</summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(365);

    public Task<CreateLinkResult> CreateAsync(string? rawTarget, CancellationToken cancellationToken = default) =>
        CreateAsync(rawTarget, alias: null, expiresAt: null, cancellationToken);

    /// <summary>
    /// Creates a link with a random code, or with <paramref name="alias"/> when one is given (AB-03b), optionally
    /// expiring at <paramref name="expiresAt"/> (AB-04).
    /// </summary>
    public async Task<CreateLinkResult> CreateAsync(
        string? rawTarget, string? alias, DateTimeOffset? expiresAt, CancellationToken cancellationToken = default)
    {
        var validation = TargetUrlValidator.Validate(rawTarget, urlPolicy);
        if (!validation.IsValid)
        {
            return CreateLinkResult.Failure(CreateLinkError.InvalidUrl, validation.Error);
        }

        var now = timeProvider.GetUtcNow();
        if (expiresAt is { } expiry && (expiry <= now || expiry > now + MaxLifetime))
        {
            return CreateLinkResult.Failure(
                CreateLinkError.InvalidExpiry, $"The expiry must be in the future and at most {MaxLifetime.TotalDays:0} days ahead.");
        }

        if (alias is not null)
        {
            var aliasCheck = AliasRules.Validate(alias, aliasPolicy);
            if (!aliasCheck.IsValid)
            {
                return CreateLinkResult.Failure(CreateLinkError.InvalidAlias, aliasCheck.Error);
            }

            // The unique index decides whether the alias is free; no silent fallback to a random code.
            var aliased = new ShortLink(aliasCheck.Alias, validation.Url, now, expiresAt);
            return await repository.TryAddAsync(aliased, cancellationToken).ConfigureAwait(false)
                ? CreateLinkResult.Success(aliased)
                : CreateLinkResult.Failure(CreateLinkError.AliasTaken, $"The alias '{aliasCheck.Alias}' is already taken.");
        }

        for (var attempt = 1; attempt <= MaxCodeAttempts; attempt++)
        {
            var link = new ShortLink(codeGenerator.Generate(), validation.Url, now, expiresAt);
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
    /// Resolves a code for a redirect and records the click. Expired or disabled links are "gone" and record nothing.
    /// The click is queued, not written here, so the redirect never waits for the database.
    /// </summary>
    public async Task<VisitResult> VisitAsync(string? code, Uri? referrer, CancellationToken cancellationToken = default)
    {
        if (!ShortCode.IsWellFormed(code))
        {
            return VisitResult.NotFound;
        }

        var target = await redirectLookup.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return VisitResult.NotFound;
        }

        var now = timeProvider.GetUtcNow();
        var status = target.StatusAt(now);
        if (status != LinkStatus.Active)
        {
            return new VisitResult(VisitOutcome.Gone, target, status);
        }

        clickRecorder.Record(new ClickEvent(target.LinkId, now, ClickEvent.ReferrerHostFrom(referrer)));
        return new VisitResult(VisitOutcome.Redirect, target);
    }

    /// <summary>Takes a link down (AB-04). Returns false when the code doesn't exist; disabling twice is fine.</summary>
    public async Task<bool> DisableAsync(string? code, CancellationToken cancellationToken = default)
    {
        if (!ShortCode.IsWellFormed(code))
        {
            return false;
        }

        var found = await repository.DisableAsync(code, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (found)
        {
            redirectLookup.Invalidate(code);   // a cached target must not keep redirecting
        }

        return found;
    }

    /// <summary>Click analytics for <paramref name="code"/>, or null when the code does not exist.</summary>
    public async Task<LinkStats?> GetStatsAsync(string? code, CancellationToken cancellationToken = default)
    {
        var link = await ResolveAsync(code, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var perDay = await statsQuery.GetClicksPerDayAsync(link.Id, today.AddDays(-(StatsDays - 1)), cancellationToken)
            .ConfigureAwait(false);
        var referrers = await statsQuery.GetTopReferrersAsync(link.Id, TopReferrerCount, cancellationToken)
            .ConfigureAwait(false);

        return new LinkStats(link, perDay, referrers);
    }
}
