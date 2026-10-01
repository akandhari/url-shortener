using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrlShortener.Core.Links;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Infrastructure.Clicks;

/// <summary>
/// The single consumer of <see cref="ClickBuffer"/>. Writes clicks in batches: inserts the events and increases
/// each link's count inside the database (ClickCount = ClickCount + n). Nothing is read, changed in memory and
/// written back, so concurrent clicks can't overwrite each other.
/// </summary>
internal sealed partial class ClickWriterService(
    ClickBuffer buffer,
    IServiceScopeFactory scopeFactory,
    IOptions<ClickRecordingOptions> options,
    ILogger<ClickWriterService> logger) : BackgroundService
{
    private readonly int _maxBatchSize = options.Value.MaxBatchSize;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // No fixed delay: write as soon as clicks arrive. Under load, clicks pile up while a batch is being
            // written, so batches grow naturally.
            while (await buffer.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                await WriteAvailableAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown: write what is still queued so normal restarts don't lose clicks.
            await WriteAvailableAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task WriteAvailableAsync(CancellationToken cancellationToken)
    {
        var batch = new List<ClickEvent>(_maxBatchSize);
        while (true)
        {
            while (batch.Count < _maxBatchSize && buffer.Reader.TryRead(out var click))
            {
                batch.Add(click);
            }

            if (batch.Count == 0)
            {
                return;
            }

            await WriteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            batch.Clear();
        }
    }

    private async Task WriteBatchAsync(List<ClickEvent> batch, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            db.ClickEvents.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            foreach (var group in batch.GroupBy(c => c.LinkId))
            {
                var linkId = group.Key;
                var clicks = group.LongCount();
                await db.Links
                    .Where(l => l.Id == linkId)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.ClickCount, l => l.ClickCount + clicks), cancellationToken)
                    .ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A failed batch must not stop the writer; it is logged and the next batch continues.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogBatchFailed(logger, ex, batch.Count);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to write a batch of {Count} clicks; the batch is skipped")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception, int count);
}
