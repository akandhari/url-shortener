using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UrlShortener.Core.Links;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.IntegrationTests.Brownfield;

/// <summary>
/// BF-09: a database created by v0.1.0 (first migration only, with existing links and counts) upgrades to the latest
/// version with its data intact, and every column v0.1.0 uses is still there unchanged, so rolling back stays
/// possible. (AB-04 adds nullable columns to Links on purpose; older versions simply don't select them.)
/// </summary>
public sealed class UpgradeFromV01Tests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public UpgradeFromV01Tests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task V01_database_upgrades_with_links_and_counts_intact()
    {
        using var db = CreateContext();
        var initialCreate = db.Database.GetMigrations().First();
        Assert.EndsWith("_InitialCreate", initialCreate, StringComparison.Ordinal);

        // The database as v0.1.0 left it: first migration only, links with counted clicks.
        await db.GetService<IMigrator>().MigrateAsync(initialCreate);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO Links (Code, TargetUrl, CreatedAt, ClickCount) VALUES
              ('abc1234', 'https://example.com/one', 0, 7),
              ('xyz9876', 'https://example.com/two', 0, 0)
            """);
        var linksColumnsBefore = await LinksColumnsAsync();

        await db.Database.MigrateAsync();   // upgrade to the latest version

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var links = await db.Links.AsNoTracking().OrderBy(l => l.Code).ToListAsync();
        Assert.Equal(["abc1234", "xyz9876"], links.Select(l => l.Code));
        Assert.Equal([7L, 0L], links.Select(l => l.ClickCount));
        Assert.Equal(new Uri("https://example.com/one"), links[0].TargetUrl);
        var linksColumnsAfter = await LinksColumnsAsync();
        Assert.All(linksColumnsBefore, column => Assert.Contains(column, linksColumnsAfter));   // v0.1 columns unchanged
        Assert.All(links, l => Assert.Equal(LinkStatus.Active, l.StatusAt(DateTimeOffset.UtcNow)));  // old links stay live

        // Old links can receive click events in the new table.
        db.ClickEvents.Add(new ClickEvent(links[0].Id, DateTimeOffset.UtcNow, null));
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.ClickEvents.CountAsync());
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private async Task<List<string>> LinksColumnsAsync()
    {
        var columns = new List<string>();
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT name || ':' || type || ':' || \"notnull\" FROM pragma_table_info('Links') ORDER BY cid";
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }
}
