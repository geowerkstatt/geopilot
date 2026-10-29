using Geopilot.Pipeline.Processes.Gpkg;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class GpkgMetadataExtractorProcessTest
{
    private GpkgMetadataExtractorProcess process;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void SetUp()
    {
        process = new GpkgMetadataExtractorProcess(NullLogger.Instance);
    }

    [TestMethod]
    public async Task ReadModelsFromSchemaGpkg()
    {
        var gpkgFile = GetTestPipelineFile("schema.gpkg");

        var result = await process.RunAsync(gpkgFile, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreEqual(GpkgFileState.Readable, result.FileState);
        CollectionAssert.AreEqual(new[] { "SimpleModel" }, result.Models);
    }

    [TestMethod]
    public async Task ReadFromXtfIsUnreadable()
    {
        var gpkgFile = GetTestPipelineFile("transfer.xtf");

        var result = await process.RunAsync(gpkgFile, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreEqual(GpkgFileState.Unreadable, result.FileState);
        Assert.IsEmpty(result.Models);
    }

    [TestMethod]
    public async Task ReadFromFileNameWithConnectionStringCharactersReleasesTheFile()
    {
        var directory = Directory.CreateTempSubdirectory("GpkgMetadata_");
        try
        {
            var path = Path.Combine(directory.FullName, "schema;Mode=ReadWriteCreate.gpkg");
            File.Copy(Path.Combine("TestData", "Ilitools", "schema.gpkg"), path);

            var result = await process.RunAsync(new PipelineFile(path, Path.GetFileName(path)), TestContext.CancellationToken);

            Assert.AreEqual(GpkgFileState.Readable, result.FileState);
            CollectionAssert.AreEqual(new[] { "SimpleModel" }, result.Models);

            // A connection left open, for example by pooling, keeps the file locked on Windows.
            File.Delete(path);
            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadFromModelTableWithUnexpectedLayoutIsUnreadable()
    {
        var directory = Directory.CreateTempSubdirectory("GpkgMetadata_");
        try
        {
            var path = Path.Combine(directory.FullName, "layout.gpkg");
            await using (var connection = await CreateDatabaseAsync(path))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE T_ILI2DB_MODEL (filename TEXT, unexpected_column TEXT)";
                await command.ExecuteNonQueryAsync(TestContext.CancellationToken);
            }

            var result = await process.RunAsync(new PipelineFile(path, Path.GetFileName(path)), TestContext.CancellationToken);

            Assert.AreEqual(GpkgFileState.Unreadable, result.FileState);
            Assert.IsEmpty(result.Models);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadFromEmptyDatabaseIsMissingData()
    {
        var directory = Directory.CreateTempSubdirectory("GpkgMetadata_");
        try
        {
            var path = Path.Combine(directory.FullName, "empty_database.gpkg");
            await using (var connection = await CreateDatabaseAsync(path))
            {
            }

            var result = await process.RunAsync(new PipelineFile(path, Path.GetFileName(path)), TestContext.CancellationToken);

            Assert.AreEqual(GpkgFileState.MissingData, result.FileState);
            Assert.IsEmpty(result.Models);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadFromEmptyModelTableIsMissingData()
    {
        var directory = Directory.CreateTempSubdirectory("GpkgMetadata_");
        try
        {
            var path = Path.Combine(directory.FullName, "empty_model_table.gpkg");
            await using (var connection = await CreateDatabaseAsync(path))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE T_ILI2DB_MODEL (
                        filename TEXT(250) NOT NULL,
                        iliversion TEXT(3) NOT NULL,
                        modelName TEXT NOT NULL,
                        content TEXT NOT NULL,
                        importDate DATETIME NOT NULL)
                    """;
                await command.ExecuteNonQueryAsync(TestContext.CancellationToken);
            }

            var result = await process.RunAsync(new PipelineFile(path, Path.GetFileName(path)), TestContext.CancellationToken);

            Assert.AreEqual(GpkgFileState.MissingData, result.FileState);
            Assert.IsEmpty(result.Models);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task InvalidModelNameIsUnreadable()
    {
        var directory = Directory.CreateTempSubdirectory("GpkgMetadata_");
        try
        {
            var path = Path.Combine(directory.FullName, "invalid_name.gpkg");
            await using (var connection = await CreateDatabaseAsync(path))
            {
                await using var create = connection.CreateCommand();
                create.CommandText = """
                    CREATE TABLE T_ILI2DB_MODEL (
                        filename TEXT(250) NOT NULL,
                        iliversion TEXT(3) NOT NULL,
                        modelName TEXT NOT NULL,
                        content TEXT NOT NULL,
                        importDate DATETIME NOT NULL)
                    """;
                await create.ExecuteNonQueryAsync(TestContext.CancellationToken);

                await using var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO T_ILI2DB_MODEL (filename, iliversion, modelName, content, importDate)
                    VALUES ('invalid_name.ili', '2.3', '_Invalid_Name', '...', CURRENT_TIMESTAMP)
                    """;
                await insert.ExecuteNonQueryAsync(TestContext.CancellationToken);
            }

            var result = await process.RunAsync(new PipelineFile(path, Path.GetFileName(path)), TestContext.CancellationToken);

            Assert.AreEqual(GpkgFileState.Unreadable, result.FileState);
            Assert.IsEmpty(result.Models);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(new[] { "Geometry_V2", "Units" }, new[] { "Geometry_V2", "Units", "Units" }, DisplayName = "Removes duplicates")]
    [DataRow(new[] { "Geometry_V2" }, new[] { "Geometry_V2{ Units}" }, DisplayName = "Ignores references")]
    [DataRow(new[] { "Geometry_V2", "GeometryCHLV03_V2", "GeometryCHLV95_V2" }, new[] { "Geometry_V2 GeometryCHLV03_V2 GeometryCHLV95_V2" }, DisplayName = "Expands multiple models in a single string")]
    [DataRow(new[] { "Geometry_V2", "GeometryCHLV03_V2", "GeometryCHLV95_V2" }, new[] { "Geometry_V2{ Units} GeometryCHLV03_V2{ Geometry_V2 CoordSys Units} GeometryCHLV95_V2{ Geometry_V2 CoordSys Units}" }, DisplayName = "Expands multiple models in a single string, ignoring references")]
    public void ExtractModelNames(string[] expected, string[] input)
    {
        var actual = GpkgMetadataExtractorProcess.ExtractModelNames(input);

        CollectionAssert.AreEquivalent(expected, actual.ToArray());
    }

    [TestMethod]
    [DataRow("Geometry_V2")]
    [DataRow("Units")]
    [DataRow("GeometryCHLV03_V2")]
    [DataRow("A")]
    public void IsValidInterlisModelName(string modelName)
    {
        Assert.IsTrue(GpkgMetadataExtractorProcess.IsValidInterlisModelName(modelName));
    }

    [TestMethod]
    [DataRow("_Geometry_V2")]
    [DataRow("1Units")]
    [DataRow("A B C")]
    [DataRow("A{B}")]
    [DataRow("0")]
    [DataRow("Ä")]
    [DataRow("Flächen")]
    [DataRow("Units\n")]
    [DataRow("\nUnits")]
    [DataRow("Un\nits")]
    public void IsInvalidInterlisModelName(string modelName)
    {
        Assert.IsFalse(GpkgMetadataExtractorProcess.IsValidInterlisModelName(modelName));
    }

    private PipelineFile GetTestPipelineFile(string name)
    {
        return new PipelineFile(Path.Combine("TestData", "Ilitools", name), name);
    }

    private async Task<SqliteConnection> CreateDatabaseAsync(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
        }.ToString();
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(TestContext.CancellationToken);
        return connection;
    }
}
