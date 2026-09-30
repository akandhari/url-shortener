using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Core.Links;

namespace UrlShortener.Infrastructure.Persistence;

internal sealed class EfLinkRepository(AppDbContext db) : ILinkRepository
{
    // SQLITE_CONSTRAINT (19) with the extended code SQLITE_CONSTRAINT_UNIQUE (2067).
    private const int SqliteConstraint = 19;
    private const int SqliteConstraintUnique = 2067;

    public async Task<bool> TryAddAsync(ShortLink link, CancellationToken cancellationToken)
    {
        db.Links.Add(link);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Detach the failed entity so the same context can be used for the retry with a new code.
            db.Entry(link).State = EntityState.Detached;
            return false;
        }
    }

    public Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        db.Links.AsNoTracking().SingleOrDefaultAsync(l => l.Code == code, cancellationToken);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraint, SqliteExtendedErrorCode: SqliteConstraintUnique };
}
