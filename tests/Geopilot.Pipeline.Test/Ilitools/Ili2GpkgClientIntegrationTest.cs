using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Geopilot.PipelineCore.Ilitools;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Geopilot.Pipeline.Test.Ilitools;

/// <summary>
/// Integration tests for the <see cref="Ili2GpkgClient"/> class, testing against an ilitools-wrapper service running locally.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public partial class Ili2GpkgClientIntegrationTest
{
    private const string DatasetName = "TestDataset";

    private enum InterlisVersion
    {
        Ili2_3,
        Ili2_4,
    }

    public TestContext TestContext { get; set; }

    private GrpcChannel grpcChannel;
    private Ili2GpkgClient ili2GpkgClient;

    [TestInitialize]
    public void SetUp()
    {
        grpcChannel = PipelineProcessFactory.CreateIlitoolsWrapperChannel("http://localhost:5555");
        ili2GpkgClient = new Ili2GpkgClient(grpcChannel, NullLogger<Ili2GpkgClient>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        grpcChannel?.Dispose();
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task SchemaImportAsync()
    {
        var modelFile = GetTestPipelineFile("model.ili");
        var gpkgFile = GetTestPipelineFile("schema_import.gpkg");
        var logFile = GetTestPipelineFile("schema_import.log");
        var xtfLogFile = GetTestPipelineFile("schema_import.xtf");
        await DeleteIfExistsAsync(gpkgFile, xtfLogFile, logFile);

        var args = new Ili2GpkgArgs
        {
            CreateBasketCol = true, // required for update
        };
        var result = await ili2GpkgClient.SchemaImportAsync(args, modelFile, gpkgFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Success, "Schema import failed. Log: " + await ReadLogAsync(logFile));
        await AssertLogWrittenAsync(logFile);
        await AssertXtfLogWrittenAsync(xtfLogFile);

        using var stream = await gpkgFile.OpenReadAsync();
        Assert.IsGreaterThan(0, stream.Length, "Resulting GPKG file is empty.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task SchemaImportAsyncFailsWithInvalidModel()
    {
        var modelFile = GetTestPipelineFile("invalid_model.ili");
        var gpkgFile = GetTestPipelineFile("schema_import_invalid.gpkg");
        var logFile = GetTestPipelineFile("schema_import_invalid.log");
        var xtfLogFile = GetTestPipelineFile("schema_import_invalid.xtf");
        await DeleteIfExistsAsync(gpkgFile, logFile, xtfLogFile);

        var args = new Ili2GpkgArgs();
        var result = await ili2GpkgClient.SchemaImportAsync(args, modelFile, gpkgFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Success, "Schema import should have failed.");
        await AssertLogWrittenAsync(logFile);
        await AssertXtfLogWrittenAsync(xtfLogFile);

        Assert.IsFalse(File.Exists(await gpkgFile.GetLocalPathAsync()), "GPKG file should not have been created for an invalid model.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task SchemaImportAsyncIntoExistingDatabase()
    {
        var modelFile = GetTestPipelineFile("model2.ili");
        var inputFile = GetTestPipelineFile("schema.gpkg");
        var outputFile = GetTestPipelineFile("schema_import_existing.gpkg");
        var logFile = GetTestPipelineFile("schema_import_existing.log");
        var xtfLogFile = GetTestPipelineFile("schema_import_existing.xtf");
        await DeleteIfExistsAsync(outputFile, logFile, xtfLogFile);

        var args = new Ili2GpkgArgs
        {
            CreateBasketCol = true,
        };
        var result = await ili2GpkgClient.SchemaImportAsync(args, modelFile, outputFile, logFile, xtfLogFile, inputFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.Success, "Schema import into existing database failed. Log: " + await ReadLogAsync(logFile));
        await AssertLogWrittenAsync(logFile);
        await AssertXtfLogWrittenAsync(xtfLogFile);

        // SimpleModel comes from the existing database, SimpleModel2 from the imported model file.
        CollectionAssert.AreEquivalent(new[] { "SimpleModel" }, await ReadModelNamesAsync(inputFile), "Precondition: Input file should only contain SimpleModel.");
        CollectionAssert.AreEquivalent(new[] { "SimpleModel", "SimpleModel2" }, await ReadModelNamesAsync(outputFile));
    }

    [TestMethod]
    public async Task ImportAsyncRequiresInputFileWithoutDoSchemaImport()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await ili2GpkgClient.ImportAsync(new Ili2GpkgArgs(), null, GetTestPipelineFile("unused.gpkg"), [GetTestPipelineFile("transfer.xtf")], GetTestPipelineFile("unused.log"), GetTestPipelineFile("unused.xtf"), cancellationToken: TestContext.CancellationToken);
        });
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ImportAsync()
    {
        var inputFile = GetTestPipelineFile("schema.gpkg");
        var outputFile = GetTestPipelineFile("import.gpkg");
        var transferFile = GetTestPipelineFile("transfer.xtf");
        var logFile = GetTestPipelineFile("import.log");
        var xtfLogFile = GetTestPipelineFile("import.xtf");
        await DeleteIfExistsAsync(outputFile, logFile, xtfLogFile);

        var args = new Ili2GpkgArgs
        {
            Dataset = DatasetName, // required for update
        };
        var result = await ili2GpkgClient.ImportAsync(args, inputFile, outputFile, [transferFile], logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Success, "Import failed. Log: " + await ReadLogAsync(logFile));
        await AssertLogWrittenAsync(logFile);
        await AssertXtfLogWrittenAsync(xtfLogFile);

        using var inputFileStream = await inputFile.OpenReadAsync();
        using var outputFileStream = await outputFile.OpenReadAsync();
        Assert.IsGreaterThan(0, outputFileStream.Length, "Resulting GPKG file is empty.");
        Assert.IsGreaterThanOrEqualTo(inputFileStream.Length, outputFileStream.Length, "Resulting GPKG file is smaller than the input file.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ImportAsyncFailsWithoutTransferFiles()
    {
        var inputFile = GetTestPipelineFile("schema.gpkg");
        var outputFile = GetTestPipelineFile("import_invalid.gpkg");
        var logFile = GetTestPipelineFile("import_invalid.log");
        var xtfLogFile = GetTestPipelineFile("import_invalid.xtf");
        await DeleteIfExistsAsync(outputFile, logFile, xtfLogFile);

        var args = new Ili2GpkgArgs();

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ili2GpkgClient.ImportAsync(args, inputFile, outputFile, [], logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.IsFalse(File.Exists(await outputFile.GetLocalPathAsync()), "GPKG file should not have been created for an invalid model.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ImportAsyncWithDoSchemaImportFromRepositoryArchive()
    {
        var archive = new PipelineFile(Path.Combine("TestData", "ModelRepository", "model-repository.zip"), "model-repository.zip");
        var transferFile = new PipelineFile(Path.Combine("TestData", "ModelRepository", "AllErrors23-ok.xtf"), "AllErrors23-ok.xtf");
        var outputFile = GetTestPipelineFile("import_repository_archive.gpkg");
        var logFile = GetTestPipelineFile("import_repository_archive.log");
        var xtfLogFile = GetTestPipelineFile("import_repository_archive.xtf");
        await DeleteIfExistsAsync(outputFile, logFile, xtfLogFile);

        // Without an input database the schema is created from scratch, and the model only exists in the archive,
        // which the service unpacks into the repository subfolder of the session.
        var args = new Ili2GpkgArgs
        {
            DoSchemaImport = true,
            DefaultSrsCode = 2056,
            ModelDirs = ["%XTF_DIR/repository"],
        };
        var result = await ili2GpkgClient.ImportAsync(args, null, outputFile, [transferFile], logFile, xtfLogFile, archive, cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.Success, "Import with schema import from the repository archive failed. Log: " + await ReadLogAsync(logFile));
        await AssertLogWrittenAsync(logFile);
        await AssertXtfLogWrittenAsync(xtfLogFile);

        CollectionAssert.AreEquivalent(new[] { "AllErrors23" }, await ReadModelNamesAsync(outputFile));
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ImportAsyncWithDoSchemaImportFromModels()
    {
        var modelFile = GetTestPipelineFile("model.ili");
        var transferFile = GetTestPipelineFile("transfer.xtf");
        var outputFile = GetTestPipelineFile("import_with_schema_import.gpkg");
        var logFile = GetTestPipelineFile("import_with_schema_import.log");
        var xtfLogFile = GetTestPipelineFile("import_with_schema_import.xtf");
        await DeleteIfExistsAsync(outputFile, logFile, xtfLogFile);

        // Without an input database the schema is created from scratch, and the model files land in the models subfolder of the session.
        var args = new Ili2GpkgArgs
        {
            DoSchemaImport = true,
            ModelDirs = ["%XTF_DIR/models"],
        };
        var result = await ili2GpkgClient.ImportAsync(args, null, outputFile, [transferFile], logFile, xtfLogFile, modelFiles: [modelFile], cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.Success, "Import with schema import from the models failed. Log: " + await ReadLogAsync(logFile));
        await AssertLogWrittenAsync(logFile);
        await AssertXtfLogWrittenAsync(xtfLogFile);

        CollectionAssert.AreEquivalent(new[] { "SimpleModel" }, await ReadModelNamesAsync(outputFile));
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ImportAsyncWithItfTransferFormat()
    {
        var modelFile = GetTestPipelineFile("model_interlis1.ili");
        var transferFile = GetTestPipelineFile("transfer_interlis1.itf");
        var outputFile = GetTestPipelineFile("import_interlis1.gpkg");
        var logFile = GetTestPipelineFile("import_interlis1.log");
        var xtfLogFile = GetTestPipelineFile("import_interlis1.xtf");
        await DeleteIfExistsAsync(outputFile, logFile, xtfLogFile);

        var args = new Ili2GpkgArgs
        {
            DoSchemaImport = true,
            TransferFormat = Ili2GpkgTransferFormat.Itf,
            ModelDirs = ["%XTF_DIR/models"],
        };
        var result = await ili2GpkgClient.ImportAsync(args, null, outputFile, [transferFile], logFile, xtfLogFile, modelFiles: [modelFile], cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.Success, "Import of INTERLIS 1 ITF transfer file failed. Log: " + await ReadLogAsync(logFile));
        await AssertLogWrittenAsync(logFile);
        await AssertXtfLogWrittenAsync(xtfLogFile);

        CollectionAssert.Contains(await ReadModelNamesAsync(outputFile), "SimpleModel1");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ExportAsync()
    {
        var dbFile = GetTestPipelineFile("data.gpkg");
        var transferFile = GetTestPipelineFile("export.xtf");
        var logFile = GetTestPipelineFile("export.log");
        var xtfLogFile = GetTestPipelineFile("export.xtflog");
        await DeleteIfExistsAsync(transferFile, logFile, xtfLogFile);

        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
        };
        var result = await ili2GpkgClient.ExportAsync(args, dbFile, transferFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.Success, "Export failed. Log: " + await ReadLogAsync(logFile));
        await AssertLogWrittenAsync(logFile);
        await AssertXtfLogWrittenAsync(xtfLogFile);

        await AssertIsInterlisTransferAsync(transferFile, InterlisVersion.Ili2_4);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ExportAsyncWithModelDirs()
    {
        var dbFile = GetTestPipelineFile("data.gpkg");
        var transferFile = GetTestPipelineFile("export_modeldirs.xtf");
        var logFile = GetTestPipelineFile("export_modeldirs.log");
        var xtfLogFile = GetTestPipelineFile("export_modeldirs.xtflog");
        await DeleteIfExistsAsync(transferFile, logFile, xtfLogFile);

        // Model dirs replace the default of the tool, so the model has to come from the GeoPackage itself.
        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
            ModelDirs = ["%ILI_FROM_DB", "%XTF_DIR"],
        };
        var result = await ili2GpkgClient.ExportAsync(args, dbFile, transferFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.Success, "Export with model dirs failed. Log: " + await ReadLogAsync(logFile));
        await AssertLogWrittenAsync(logFile);
        await AssertXtfLogWrittenAsync(xtfLogFile);

        await AssertIsInterlisTransferAsync(transferFile, InterlisVersion.Ili2_4);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ExportAsyncFailsWithLocalPathAsModelDir()
    {
        var dbFile = GetTestPipelineFile("data.gpkg");
        var transferFile = GetTestPipelineFile("export_invalid_modeldir.xtf");
        var logFile = GetTestPipelineFile("export_invalid_modeldir.log");
        var xtfLogFile = GetTestPipelineFile("export_invalid_modeldir.xtflog");
        await DeleteIfExistsAsync(transferFile, logFile, xtfLogFile);

        // Only http(s) URLs and the tool placeholders are accepted, so the wrapper has to reject a local path.
        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
            ModelDirs = ["/etc/models"],
        };

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ili2GpkgClient.ExportAsync(args, dbFile, transferFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ExportAsyncFailsWithMetaConfigFilePath()
    {
        var dbFile = GetTestPipelineFile("data.gpkg");
        var transferFile = GetTestPipelineFile("export_invalid_metaconfig.xtf");
        var logFile = GetTestPipelineFile("export_invalid_metaconfig.log");
        var xtfLogFile = GetTestPipelineFile("export_invalid_metaconfig.xtflog");
        await DeleteIfExistsAsync(transferFile, logFile, xtfLogFile);

        // Only the repository indexed form ilidata:<DatasetId> is accepted, not a file path.
        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
            MetaConfig = "profile.toml",
        };

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ili2GpkgClient.ExportAsync(args, dbFile, transferFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task UpdateAsync()
    {
        var inputFile = GetTestPipelineFile("data.gpkg");
        var outputFile = GetTestPipelineFile("update.gpkg");
        var transferFile = GetTestPipelineFile("transfer.xtf");
        var logFile = GetTestPipelineFile("update.log");
        var xtfLogFile = GetTestPipelineFile("update.xtf");
        await DeleteIfExistsAsync(outputFile, logFile, xtfLogFile);

        var args = new Ili2GpkgArgs
        {
            Dataset = DatasetName,
        };
        var result = await ili2GpkgClient.UpdateAsync(args, inputFile, outputFile, [transferFile], logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.Success, "Update failed. Log: " + await ReadLogAsync(logFile));
        await AssertLogWrittenAsync(logFile);
        await AssertXtfLogWrittenAsync(xtfLogFile);

        using var inputFileStream = await inputFile.OpenReadAsync();
        using var outputFileStream = await outputFile.OpenReadAsync();
        Assert.IsGreaterThan(0, outputFileStream.Length, "Resulting GPKG file is empty.");
        Assert.IsGreaterThanOrEqualTo(inputFileStream.Length, outputFileStream.Length, "Resulting GPKG file is smaller than the input file.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task UpdateAsyncFailsWithoutTransferFiles()
    {
        var inputFile = GetTestPipelineFile("data.gpkg");
        var outputFile = GetTestPipelineFile("update_invalid.gpkg");
        var logFile = GetTestPipelineFile("update_invalid.log");
        var xtfLogFile = GetTestPipelineFile("update_invalid.xtf");
        await DeleteIfExistsAsync(outputFile, logFile, xtfLogFile);

        var args = new Ili2GpkgArgs
        {
            Dataset = DatasetName,
        };

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ili2GpkgClient.UpdateAsync(args, inputFile, outputFile, [], logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.IsFalse(File.Exists(await outputFile.GetLocalPathAsync()), "GPKG file should not have been created.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ValidateAsync()
    {
        var inputFile = GetTestPipelineFile("data.gpkg");
        var logFile = GetTestPipelineFile("log_success.log");
        var xtfLogFile = GetTestPipelineFile("log_success.xtf");
        await DeleteIfExistsAsync(logFile, xtfLogFile);

        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
        };

        var result = await ili2GpkgClient.ValidateAsync(args, inputFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.Success, "Validation failed. Log: " + await ReadLogAsync(logFile));
        await AssertLogWrittenAsync(logFile);
        await AssertXtfLogWrittenAsync(xtfLogFile);

        await AssertIsInterlisTransferAsync(xtfLogFile, InterlisVersion.Ili2_3);
    }

    // The ili2gpkg processes turn the lines asserted below into an error of their own, because the tool exits like
    // a failed validation. If a tool update rewords them, these assertions are what say so.
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncReportsAnUnresolvableMetaConfigInTheLog()
    {
        var log = await ValidateWithUnresolvableConfigAsync("validate_unknown_metaconfig", new Ili2GpkgArgs { MetaConfig = "ilidata:DOESNOTEXIST" });

        Assert.Contains(IlitoolsHelper.Ili2gpkgMetaConfigNotFoundMarker, log);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncReportsAnUnresolvableValidConfigInTheLog()
    {
        var log = await ValidateWithUnresolvableConfigAsync("validate_unknown_validconfig", new Ili2GpkgArgs { ValidConfig = "ilidata:DOESNOTEXIST" });

        Assert.Contains(IlitoolsHelper.ValidConfigNotFoundMarker("ilidata:DOESNOTEXIST"), log);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncFailsWithAnUnknownPluginId()
    {
        var inputFile = GetTestPipelineFile("data.gpkg");
        var logFile = GetTestPipelineFile("log_unknown_plugin.log");
        var xtfLogFile = GetTestPipelineFile("log_unknown_plugin.xtf");
        await DeleteIfExistsAsync(logFile, xtfLogFile);

        // The wrapper offers only what its plugin directory holds, which is empty in the compose image, so every id
        // is unknown and has to be rejected. That rejection is what proves the deployed image understands the field
        // at all: proto3 drops an unknown field silently, so an image without it would validate as if no plugin had
        // been selected and report success. This test therefore guards the pairing of the stub version referenced
        // here against the wrapper image the compose file pins.
        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
            PluginIds = ["no-such-plugin"],
        };

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ili2GpkgClient.ValidateAsync(args, inputFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncFailsWithAnUnknownToolVersion()
    {
        var inputFile = GetTestPipelineFile("data.gpkg");
        var logFile = GetTestPipelineFile("log_unknown_version.log");
        var xtfLogFile = GetTestPipelineFile("log_unknown_version.xtf");
        await DeleteIfExistsAsync(logFile, xtfLogFile);

        // The wrapper offers the versions its image ships, so a version it does not offer has to be rejected. As
        // with the plugin id above, that rejection is what proves the deployed image understands the field at all:
        // proto3 drops an unknown field silently, so an older image would run its default version and report
        // success. This test therefore guards the pairing of the stub version referenced here against the wrapper
        // image the compose file pins.
        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
            ToolVersion = "0.0.0-no-such-version",
        };

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ili2GpkgClient.ValidateAsync(args, inputFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncCreatesXtfLogOnError()
    {
        var inputFile = GetTestPipelineFile("data_error.gpkg"); // contains a name that is too short
        var logFile = GetTestPipelineFile("log_error.log");
        var xtfLogFile = GetTestPipelineFile("log_error.xtf");
        await DeleteIfExistsAsync(logFile, xtfLogFile);

        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
        };

        var result = await ili2GpkgClient.ValidateAsync(args, inputFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsFalse(result.Success, "Validation should have failed.");
        Assert.MatchesRegex(MandatoryConstraintErrorRegex(), await ReadLogAsync(logFile));

        await AssertIsInterlisTransferAsync(xtfLogFile, InterlisVersion.Ili2_3);
    }

    private async Task DeleteIfExistsAsync(params PipelineFile[] files)
    {
        foreach (var file in files)
        {
            var path = await file.GetLocalPathAsync(TestContext.CancellationToken);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private async Task<string> ValidateWithUnresolvableConfigAsync(string outputName, Ili2GpkgArgs configArgs)
    {
        var inputFile = GetTestPipelineFile("data.gpkg");
        var logFile = GetTestPipelineFile($"{outputName}.log");
        var xtfLogFile = GetTestPipelineFile($"{outputName}.xtf");
        await DeleteIfExistsAsync(logFile, xtfLogFile);

        // Models from the GeoPackage and the session directory only, so the tool fails on the configuration without
        // asking a remote repository.
        var args = configArgs with { Models = ["SimpleModel"], ModelDirs = ["%ILI_FROM_DB", "%XTF_DIR"] };

        var result = await ili2GpkgClient.ValidateAsync(args, inputFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

        var log = await ReadLogAsync(logFile);
        Assert.IsFalse(result.Success, "The validation should fail on the configuration. Log: " + log);
        return log;
    }

    private async Task<string> ReadLogAsync(PipelineFile logFile)
    {
        var path = await logFile.GetLocalPathAsync(TestContext.CancellationToken);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, TestContext.CancellationToken) : string.Empty;
    }

    private async Task AssertLogWrittenAsync(PipelineFile logFile)
    {
        // ili2gpkg prefixes its log lines with the level, which also tells the log apart from an xtf log routed to the wrong file.
        Assert.Contains("Info:", await ReadLogAsync(logFile), StringComparison.OrdinalIgnoreCase, "The ili2gpkg log was not written.");
    }

    private async Task AssertXtfLogWrittenAsync(PipelineFile xtfLogFile)
    {
        await AssertIsInterlisTransferAsync(xtfLogFile, InterlisVersion.Ili2_3);
    }

    private async Task<List<string>> ReadModelNamesAsync(PipelineFile gpkgFile)
    {
        // Pooling off so no handle keeps the file locked for the next run's cleanup.
        var path = await gpkgFile.GetLocalPathAsync(TestContext.CancellationToken);
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(TestContext.CancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT modelName FROM T_ILI2DB_MODEL";
        using var reader = await command.ExecuteReaderAsync(TestContext.CancellationToken);

        var modelNames = new List<string>();
        while (await reader.ReadAsync(TestContext.CancellationToken))
        {
            modelNames.Add(reader.GetString(0));
        }

        return modelNames;
    }

    private PipelineFile GetTestPipelineFile(string name)
    {
        return new PipelineFile(Path.Combine("TestData", "Ilitools", name), name);
    }

    private async Task AssertIsInterlisTransferAsync(PipelineFile transferFile, InterlisVersion version)
    {
        using var stream = await transferFile.OpenReadAsync();
        Assert.IsGreaterThan(0, stream.Length, "Resulting XTF file is empty.");

        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken: TestContext.CancellationToken);
        Assert.IsNotNull(document.Root, "Root element of the XTF file is null.");

        var expectedName = version switch
        {
            InterlisVersion.Ili2_4 => XNamespace.Get("http://www.interlis.ch/xtf/2.4/INTERLIS") + "transfer",
            InterlisVersion.Ili2_3 => XNamespace.Get("http://www.interlis.ch/INTERLIS2.3") + "TRANSFER",
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, null),
        };
        Assert.AreEqual(expectedName, document.Root.Name);
    }

    [GeneratedRegex("^Error:.+? Mandatory Constraint SimpleModel\\.Testdata\\.ClassA\\.NameMinLength", RegexOptions.Multiline)]
    private static partial Regex MandatoryConstraintErrorRegex();
}
