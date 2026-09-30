using Microsoft.EntityFrameworkCore;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Persistence;

public class MigrationTests
{
    [Fact]
    public void Migrations_apply_to_an_empty_database()
    {
        using var database = new SqliteTestDatabase();   // migrates in its constructor
        using var db = database.CreateContext();

        Assert.Empty(db.Database.GetPendingMigrations());
        Assert.Contains("InitialCreate", db.Database.GetAppliedMigrations().Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void Model_has_no_changes_missing_from_the_migrations()
    {
        // Fails when someone changes the entity mapping but forgets to add a migration.
        using var database = new SqliteTestDatabase();
        using var db = database.CreateContext();

        Assert.False(db.Database.HasPendingModelChanges());
    }
}
