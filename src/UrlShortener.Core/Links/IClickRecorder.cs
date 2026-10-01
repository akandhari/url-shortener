namespace UrlShortener.Core.Links;

/// <summary>Records that a visitor followed a short link.</summary>
public interface IClickRecorder
{
    Task RecordAsync(ShortLink link, CancellationToken cancellationToken);
}
