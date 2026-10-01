namespace UrlShortener.Core.Links;

public enum VisitOutcome
{
    NotFound,
    Redirect,
    Gone,
}

/// <summary>Outcome of following a short link: redirect, gone (expired or disabled), or not found.</summary>
public sealed record VisitResult(VisitOutcome Outcome, RedirectTarget? Target = null, LinkStatus? GoneBecause = null)
{
    public static readonly VisitResult NotFound = new(VisitOutcome.NotFound);
}
