using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.IntegrationTests.Support;

/// <summary>
/// A real SQLite database in memory, migrated with the real migrations. It lives as long as the connection is
/// open, so each test gets a fresh, isolated database and uses the real SQL engine and constraints.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.Migrate();
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
