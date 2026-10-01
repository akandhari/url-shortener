namespace UrlShortener.Core.Links;

/// <summary>Records that a visitor followed a short link.</summary>
public interface IClickRecorder
{
    /// <summary>
    /// Hands the click over for storage. Must not block the redirect: implementations queue the event and return.
    /// </summary>
    void Record(ClickEvent click);
}
