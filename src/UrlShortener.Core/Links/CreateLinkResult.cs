using System.Diagnostics.CodeAnalysis;

namespace UrlShortener.Core.Links;

public enum CreateLinkError
{
    None,
    InvalidUrl,
    NoUniqueCode,
    InvalidAlias,
    AliasTaken,
}

/// <summary>
/// Outcome of creating a link. Invalid input is an expected outcome, so it is returned rather than thrown.
/// </summary>
public sealed record CreateLinkResult
{
    private CreateLinkResult(ShortLink? link, CreateLinkError error, string? message)
    {
        Link = link;
        Error = error;
        Message = message;
    }

    public ShortLink? Link { get; }

    public CreateLinkError Error { get; }

    public string? Message { get; }

    [MemberNotNullWhen(true, nameof(Link))]
    [MemberNotNullWhen(false, nameof(Message))]
    public bool IsSuccess => Link is not null;

    public static CreateLinkResult Success(ShortLink link) => new(link, CreateLinkError.None, null);

    public static CreateLinkResult Failure(CreateLinkError error, string message) => new(null, error, message);
}
