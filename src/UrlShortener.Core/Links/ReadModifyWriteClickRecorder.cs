namespace UrlShortener.Core.Links;

/// <summary>
/// The v0.1 behaviour, moved behind <see cref="IClickRecorder"/> without change: increment the count in memory
/// and save the row. Concurrent redirects can overwrite each other's count (lost update, see BF-03).
/// Replaced in BF-06.
/// </summary>
public sealed class ReadModifyWriteClickRecorder(ILinkRepository repository) : IClickRecorder
{
    public async Task RecordAsync(ShortLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);

        link.RegisterClick();
        await repository.UpdateAsync(link, cancellationToken).ConfigureAwait(false);
    }
}
