using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace Geopilot.Api;

[TestClass]
public class RenamePipelineRunsToRecordingsTest
{
    // Counts the recordings that still reach a row in every child table through the renamed key columns.
    private const string LinkedRecordingCount = """
        SELECT count(*)::int AS "Value"
        FROM "Recordings" r
        JOIN "RecordingFiles" f ON f."RecordingId" = r."Id"
        JOIN "RecordingReactionEvents" e ON e."RecordingId" = r."Id"
        JOIN "RecordingSteps" s ON s."RecordingId" = r."Id"
        JOIN "RecordingInputs" i ON i."RecordingStepId" = s."Id"
        JOIN "RecordingConditions" c ON c."RecordingStepId" = s."Id"
        JOIN "RecordingArtifacts" a ON a."RecordingStepId" = s."Id"
        """;

    private const string SchemaNames = """
        SELECT c.conname::text AS "Value" FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace WHERE n.nspname = 'public'
        UNION ALL SELECT indexname::text FROM pg_indexes WHERE schemaname = 'public'
        UNION ALL SELECT table_name::text FROM information_schema.tables WHERE table_schema = 'public'
        UNION ALL SELECT column_name::text FROM information_schema.columns WHERE table_schema = 'public'
        """;

    // Raw SQL against the schema before the rename, since the model only knows the new names.
    private const string InsertOneRecording = """
        INSERT INTO "PipelineRuns" ("JobId", "PipelineId", "Definition", "AppVersion", "ClientKind", "UploadId", "UploadStorageLocation", "UploadInitiatedAt", "StartedAt", "ScanState")
            VALUES (gen_random_uuid(), 'pipeline', jsonb_build_object(), '3.1', 'WebClient', gen_random_uuid(), 'Cloud', now(), now(), 'Clean');
        INSERT INTO "PipelineRunFiles" ("PipelineRunId", "FileName", "StorageKey", "DeclaredSize") SELECT "Id", 'data.xtf', 'uploads/data.xtf', 1 FROM "PipelineRuns";
        INSERT INTO "PipelineRunReactionEvents" ("PipelineRunId", "DeliveryId", "ReactionId", "ReactionKind", "Implementation", "Event", "OccurredAt")
            SELECT "Id", 1, 'mail', 'Mail', 'Mail', 'Started', now() FROM "PipelineRuns";
        INSERT INTO "PipelineRunSteps" ("PipelineRunId", "Order", "StepId", "DisplayName", "ProcessImplementation", "State")
            SELECT "Id", 0, 'validation', jsonb_build_object('de', 'Validierung'), 'Validation', 'Success' FROM "PipelineRuns";
        INSERT INTO "PipelineRunInputs" ("PipelineRunStepId", "Parameter", "Origin", "Kind") SELECT "Id", 'file', 'Upload', 'File' FROM "PipelineRunSteps";
        INSERT INTO "PipelineRunConditions" ("PipelineRunStepId", "Phase", "Kind", "Expression", "Matched") SELECT "Id", 'Post', 'Fail', 'false', false FROM "PipelineRunSteps";
        INSERT INTO "PipelineRunArtifacts" ("PipelineRunStepId", "Kind", "OriginalFileName", "PersistedFileName") SELECT "Id", 'Download', 'log.txt', 'validation_log.txt' FROM "PipelineRunSteps";
        """;

    private static readonly string[] OldTables =
        ["PipelineRuns", "PipelineRunFiles", "PipelineRunSteps", "PipelineRunInputs", "PipelineRunConditions", "PipelineRunArtifacts", "PipelineRunReactionEvents"];

    [TestMethod]
    public async Task UpKeepsRecordingsAndDownRestoresThem()
    {
        var connectionString = TestDatabaseFixture.ConnectionString.Replace(
            "Database=geopilot-test",
            "Database=geopilot-migration-test-recordings",
            StringComparison.Ordinal);

        await using var context = CreateContext(connectionString);
        await context.Database.EnsureDeletedAsync();
        try
        {
            await context.GetService<IRelationalDatabaseCreator>().CreateAsync();
            var migrator = context.GetService<IMigrator>();
            var migrations = context.Database.GetMigrations().ToList();
            var renameMigration = migrations.Single(id => id.EndsWith("_RenamePipelineRunsToRecordings", StringComparison.Ordinal));
            var previousMigration = migrations[migrations.IndexOf(renameMigration) - 1];

            await migrator.MigrateAsync(previousMigration);
            await context.Database.ExecuteSqlRawAsync(InsertOneRecording);

            await migrator.MigrateAsync(renameMigration);
            Assert.AreEqual(1, await Query<int>(context, LinkedRecordingCount).SingleAsync());
            await AssertNoSchemaNameContainsAsync(context, "PipelineRun");

            await migrator.MigrateAsync(previousMigration);
            foreach (var table in OldTables)
            {
                Assert.AreEqual(1, await Query<int>(context, $"SELECT count(*)::int AS \"Value\" FROM \"{table}\"").SingleAsync(), table);
            }

            await AssertNoSchemaNameContainsAsync(context, "Recording");

            await migrator.MigrateAsync();
            Assert.AreEqual(0, context.Database.GetPendingMigrations().Count());
            Assert.AreEqual(1, await Query<int>(context, LinkedRecordingCount).SingleAsync());
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static async Task AssertNoSchemaNameContainsAsync(Context context, string part)
    {
        var names = await Query<string>(context, SchemaNames).ToListAsync();
        var hits = names.Where(name => name.Contains(part, StringComparison.Ordinal)).ToList();
        Assert.IsEmpty(hits, string.Join(", ", hits));
    }

    private static IQueryable<T> Query<T>(Context context, string sql) => context.Database.SqlQueryRaw<T>(sql);

    private static Context CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<Context>()
            .UseNpgsql(connectionString, o => o.UseNetTopologySuite())
            .Options;

        return new Context(options);
    }
}
