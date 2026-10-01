using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace UrlShortener.IntegrationTests.Support;

/// <summary>
/// Runs the real API in memory against its own in-memory SQLite database, so tests never touch a file on disk.
/// A named, shared-cache database lives as long as at least one connection is open, hence the keep-alive connection.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Data Source=api-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;

    public ApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    // Tests that are not about rate limiting get generous limits so they never trip over them.
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Links", _connectionString)
            .UseSetting("RateLimits:Create:PermitLimit", "100000")
            .UseSetting("RateLimits:Redirect:PermitLimit", "100000")
            .UseSetting("RateLimits:Lookup:PermitLimit", "100000");

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}
