using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Geopilot.Api;

[TestClass]
public class MigrateDatabaseTest
{
    [TestMethod]
    [DataRow(false, DisplayName = "Without PostGIS, EF Core migrator")]
    [DataRow(true, DisplayName = "With PostGIS, idempotent script")]
    public async Task MigrateDatabaseConcurrentStartsMigrateOnce(bool postgisInstalled)
    {
        // A database of its own per branch, so the rows cannot interfere with each other or the shared test database.
        var connectionString = TestDatabaseFixture.ConnectionString.Replace(
            "Database=geopilot-test",
            $"Database=geopilot-migration-test-{(postgisInstalled ? "postgis" : "plain")}",
            StringComparison.Ordinal);

        using var setupContext = CreateContext(connectionString);
        setupContext.Database.EnsureDeleted();
        try
        {
            setupContext.GetService<IRelationalDatabaseCreator>().Create();
            if (postgisInstalled)
            {
                setupContext.Database.ExecuteSql($"CREATE EXTENSION postgis");
            }

            await Task.WhenAll(
                Task.Run(() => MigrateWithOwnContext(connectionString)),
                Task.Run(() => MigrateWithOwnContext(connectionString)));

            Assert.AreEqual(0, setupContext.Database.GetPendingMigrations().Count());
            CollectionAssert.AreEqual(
                setupContext.Database.GetMigrations().ToList(),
                setupContext.Database.GetAppliedMigrations().ToList());
        }
        finally
        {
            setupContext.Database.EnsureDeleted();
        }
    }

    private static void MigrateWithOwnContext(string connectionString)
    {
        using var context = CreateContext(connectionString);
        context.MigrateDatabase();
    }

    private static Context CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<Context>()
            .UseNpgsql(connectionString, o => o.UseNetTopologySuite())
            .Options;

        return new Context(options);
    }
}
