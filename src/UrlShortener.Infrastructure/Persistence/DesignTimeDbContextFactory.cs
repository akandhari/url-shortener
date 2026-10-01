using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UrlShortener.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet-ef tool to create migrations, so the tool does not need to start the API.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
