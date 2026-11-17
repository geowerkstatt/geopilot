using Geopilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

    [TestMethod]
    public void RenameDeclareColumnsKeepsThePermissionOfEveryRun()
    {
        const string before = "20261111134822_AddClientReferenceAndListIndexesToPipelineRun";
        var connectionString = TestDatabaseFixture.ConnectionString.Replace(
            "Database=geopilot-test",
            "Database=geopilot-migration-test-permission",
            StringComparison.Ordinal);

        using var context = CreateContext(connectionString);
        context.Database.EnsureDeleted();
        try
        {
            var migrator = context.GetService<IMigrator>();
            migrator.Migrate(before);
            foreach (var declarability in new[] { "Declarable", "NotDeclarable", null })
            {
                context.Database.ExecuteSql($"""
                    INSERT INTO "PipelineRuns" ("JobId", "PipelineId", "AppVersion", "ClientKind", "Definition", "UploadId",
                        "UploadStorageLocation", "UploadInitiatedAt", "StartedAt", "ScanState", "Declarability")
                    VALUES ({Guid.NewGuid()}, 'pipeline', '1.0', 'WebClient', {"{}"}::jsonb, {Guid.NewGuid()},
                        'storage', now(), now(), 'Clean', {declarability})
                    """);
            }

            migrator.Migrate();
            CollectionAssert.AreEqual(
                new PermissionToDeliver?[] { PermissionToDeliver.Permitted, PermissionToDeliver.NotPermitted, null },
                context.PipelineRuns.OrderBy(run => run.Id).Select(run => run.PermissionToDeliver).ToList());

            migrator.Migrate(before);
            CollectionAssert.AreEqual(
                new[] { "Declarable", "NotDeclarable", null },
                context.Database.SqlQueryRaw<string?>("""SELECT "Declarability" AS "Value" FROM "PipelineRuns" ORDER BY "Id" """).ToList());
        }
        finally
        {
            context.Database.EnsureDeleted();
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
