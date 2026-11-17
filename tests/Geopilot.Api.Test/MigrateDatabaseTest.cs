using Geopilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json.Nodes;

namespace Geopilot.Api;

[TestClass]
public class MigrateDatabaseTest
{
    private const string BeforeRenameDeclareColumns = "20261111134822_AddClientReferenceAndListIndexesToPipelineRun";

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
        => OnDatabaseBeforeRenameDeclareColumns("permission", (context, migrator) =>
        {
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

            migrator.Migrate(BeforeRenameDeclareColumns);
            CollectionAssert.AreEqual(
                new[] { "Declarable", "NotDeclarable", null },
                context.Database.SqlQueryRaw<string?>("""SELECT "Declarability" AS "Value" FROM "PipelineRuns" ORDER BY "Id" """).ToList());
        });

    [TestMethod]
    public void RenameDeclareColumnsRenamesItsConstraints()
        => OnDatabaseBeforeRenameDeclareColumns("constraints", (context, migrator) =>
        {
            string[] declaring = ["CK_Deliveries_Declarer", "CK_PipelineRuns_Declarer", "FK_Deliveries_MachineClients_DeclaringClientId", "FK_Deliveries_Users_DeclaringUserId"];
            string[] delivered = ["CK_Deliveries_DeliveredBy", "CK_PipelineRuns_StartedBy", "FK_Deliveries_MachineClients_DeliveredByClientId", "FK_Deliveries_Users_DeliveredByUserId"];
            var both = declaring.Concat(delivered).ToArray();
            List<string> ReadConstraints() => context.Database
                .SqlQuery<string>($"""SELECT conname::text AS "Value" FROM pg_constraint WHERE conname::text = ANY({both}) ORDER BY conname""")
                .ToList();

            migrator.Migrate();
            CollectionAssert.AreEqual(delivered, ReadConstraints());

            migrator.Migrate(BeforeRenameDeclareColumns);
            CollectionAssert.AreEqual(declaring, ReadConstraints());
        });

    [TestMethod]
    public void RenameDeclareColumnsRenamesTheSupplierValueOfTheMailChannel()
        => OnDatabaseBeforeRenameDeclareColumns("mandate-value", (context, migrator) =>
        {
            const string declarerValues = """{"info": {"to": ["info@example.com"], "includeDeclarer": true}, "receipt": {"to": ["receipt@example.com"]}}""";
            const string supplierValues = """{"info": {"to": ["info@example.com"], "includeSupplier": true}, "receipt": {"to": ["receipt@example.com"]}}""";
            const string declarerSnapshot = """{"config": {"subject": "Info", "includeDeclarer": false}, "mandate": {"includeDeclarer": true}}""";
            const string supplierSnapshot = """{"config": {"subject": "Info", "includeSupplier": false}, "mandate": {"includeSupplier": true}}""";
            context.Database.ExecuteSql($"""INSERT INTO "Users" ("Id", "AuthIdentifier") VALUES (1, 'user')""");
            context.Database.ExecuteSql($"""
                INSERT INTO "Mandates" ("Id", "FileTypes", "SpatialExtent", "ReactionSetValues")
                VALUES (1, ARRAY['.xtf'], 'POINT(0 0)'::geometry, {declarerValues}::jsonb)
                """);
            context.Database.ExecuteSql($"""
                INSERT INTO "Deliveries" ("Id", "JobId", "Date", "MandateId", "DeclaringUserId")
                VALUES (1, {Guid.NewGuid()}, now(), 1, 1)
                """);
            context.Database.ExecuteSql($"""
                INSERT INTO "DeliveryReactions" ("DeliveryId", "ReactionId", "Kind", "Implementation", "Config", "State", "Attempts", "NextAttemptAt")
                VALUES (1, 'info', 'Notification', 'Mail', {declarerSnapshot}::jsonb, 'Pending', 0, now())
                """);

            migrator.Migrate();
            AssertStoredJson(context, supplierValues, $"""SELECT "ReactionSetValues"::text AS "Value" FROM "Mandates" """);
            AssertStoredJson(context, supplierSnapshot, $"""SELECT "Config"::text AS "Value" FROM "DeliveryReactions" """);

            migrator.Migrate(BeforeRenameDeclareColumns);
            AssertStoredJson(context, declarerValues, $"""SELECT "ReactionSetValues"::text AS "Value" FROM "Mandates" """);
            AssertStoredJson(context, declarerSnapshot, $"""SELECT "Config"::text AS "Value" FROM "DeliveryReactions" """);
        });

    private static void OnDatabaseBeforeRenameDeclareColumns(string databaseSuffix, Action<Context, IMigrator> test)
    {
        var connectionString = TestDatabaseFixture.ConnectionString.Replace(
            "Database=geopilot-test",
            $"Database=geopilot-migration-test-{databaseSuffix}",
            StringComparison.Ordinal);

        using var context = CreateContext(connectionString);
        context.Database.EnsureDeleted();
        try
        {
            var migrator = context.GetService<IMigrator>();
            migrator.Migrate(BeforeRenameDeclareColumns);
            test(context, migrator);
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    private static void AssertStoredJson(Context context, string expected, FormattableString query)
    {
        var stored = context.Database.SqlQuery<string>(query).Single();
        Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(stored)), $"Stored: {stored}");
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
