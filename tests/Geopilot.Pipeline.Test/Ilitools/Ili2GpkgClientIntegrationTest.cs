using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Ilitools;
using Grpc.Core;
using Grpc.Net.Client;
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
        grpcChannel = GrpcChannel.ForAddress("http://localhost:5555");
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
        await DeleteIfExistsAsync(gpkgFile);

        var args = new Ili2GpkgArgs
        {
            CreateBasketCol = true, // required for update
        };
        var result = await ili2GpkgClient.SchemaImportAsync(args, modelFile, gpkgFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Log);
        Assert.IsNotEmpty(result.Log);
        Assert.IsTrue(result.Success, "Schema import failed. Log: " + result.Log);

        using var stream = await gpkgFile.OpenReadAsync();
        Assert.IsGreaterThan(0, stream.Length, "Resulting GPKG file is empty.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task SchemaImportAsyncFailsWithInvalidModel()
    {
        var modelFile = GetTestPipelineFile("invalid_model.ili");
        var gpkgFile = GetTestPipelineFile("schema_import_invalid.gpkg");
        await DeleteIfExistsAsync(gpkgFile);

        var args = new Ili2GpkgArgs();
        var result = await ili2GpkgClient.SchemaImportAsync(args, modelFile, gpkgFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Log);
        Assert.IsNotEmpty(result.Log);
        Assert.IsFalse(result.Success, "Schema import should have failed. Log: " + result.Log);

        Assert.IsFalse(File.Exists(await gpkgFile.GetLocalPathAsync()), "GPKG file should not have been created for an invalid model.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ImportAsync()
    {
        var inputFile = GetTestPipelineFile("schema.gpkg");
        var outputFile = GetTestPipelineFile("import.gpkg");
        var transferFile = GetTestPipelineFile("transfer.xtf");
        await DeleteIfExistsAsync(outputFile);

        var args = new Ili2GpkgArgs
        {
            Dataset = DatasetName, // required for update
        };
        var result = await ili2GpkgClient.ImportAsync(args, inputFile, outputFile, [transferFile], cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Log);
        Assert.IsNotEmpty(result.Log);
        Assert.IsTrue(result.Success, "Import failed. Log: " + result.Log);

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
        await DeleteIfExistsAsync(outputFile);

        var args = new Ili2GpkgArgs();

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ili2GpkgClient.ImportAsync(args, inputFile, outputFile, [], cancellationToken: TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.IsFalse(File.Exists(await outputFile.GetLocalPathAsync()), "GPKG file should not have been created for an invalid model.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ExportAsync()
    {
        var dbFile = GetTestPipelineFile("data.gpkg");
        var transferFile = GetTestPipelineFile("export.xtf");
        await DeleteIfExistsAsync(transferFile);

        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
        };
        var result = await ili2GpkgClient.ExportAsync(args, dbFile, transferFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Log);
        Assert.IsNotEmpty(result.Log);
        Assert.IsTrue(result.Success, "Export failed. Log: " + result.Log);

        await AssertIsInterlisTransferAsync(transferFile, InterlisVersion.Ili2_4);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ExportAsyncWithModelDirs()
    {
        var dbFile = GetTestPipelineFile("data.gpkg");
        var transferFile = GetTestPipelineFile("export_modeldirs.xtf");
        await DeleteIfExistsAsync(transferFile);

        // Model dirs replace the default of the tool, so the model has to come from the GeoPackage itself.
        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
            ModelDirs = ["%ILI_FROM_DB", "%XTF_DIR"],
        };
        var result = await ili2GpkgClient.ExportAsync(args, dbFile, transferFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Success, "Export with model dirs failed. Log: " + result.Log);

        await AssertIsInterlisTransferAsync(transferFile, InterlisVersion.Ili2_4);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ExportAsyncFailsWithLocalPathAsModelDir()
    {
        var dbFile = GetTestPipelineFile("data.gpkg");
        var transferFile = GetTestPipelineFile("export_invalid_modeldir.xtf");
        await DeleteIfExistsAsync(transferFile);

        // Only http(s) URLs and the tool placeholders are accepted, so the wrapper has to reject a local path.
        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
            ModelDirs = ["/etc/models"],
        };

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ili2GpkgClient.ExportAsync(args, dbFile, transferFile, cancellationToken: TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ExportAsyncFailsWithMetaConfigFilePath()
    {
        var dbFile = GetTestPipelineFile("data.gpkg");
        var transferFile = GetTestPipelineFile("export_invalid_metaconfig.xtf");
        await DeleteIfExistsAsync(transferFile);

        // Only the repository indexed form ilidata:<DatasetId> is accepted, not a file path.
        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
            MetaConfig = "profile.toml",
        };

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ili2GpkgClient.ExportAsync(args, dbFile, transferFile, cancellationToken: TestContext.CancellationToken);
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
        await DeleteIfExistsAsync(outputFile);

        var args = new Ili2GpkgArgs
        {
            Dataset = DatasetName,
        };
        var result = await ili2GpkgClient.UpdateAsync(args, inputFile, outputFile, [transferFile], cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Log);
        Assert.IsNotEmpty(result.Log);
        Assert.IsTrue(result.Success, "Update failed. Log: " + result.Log);

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
        await DeleteIfExistsAsync(outputFile);

        var args = new Ili2GpkgArgs
        {
            Dataset = DatasetName,
        };

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ili2GpkgClient.UpdateAsync(args, inputFile, outputFile, [], cancellationToken: TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.IsFalse(File.Exists(await outputFile.GetLocalPathAsync()), "GPKG file should not have been created.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ValidateAsync()
    {
        var inputFile = GetTestPipelineFile("data.gpkg");
        var xtfLogFile = GetTestPipelineFile("log_success.xtf");
        await DeleteIfExistsAsync(xtfLogFile);

        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
        };

        var result = await ili2GpkgClient.ValidateAsync(args, inputFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Log);
        Assert.IsNotEmpty(result.Log);
        Assert.IsTrue(result.Success, "Validation failed. Log: " + result.Log);

        await AssertIsInterlisTransferAsync(xtfLogFile, InterlisVersion.Ili2_3);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncCreatesXtfLogOnError()
    {
        var inputFile = GetTestPipelineFile("data_error.gpkg"); // contains a name that is too short
        var xtfLogFile = GetTestPipelineFile("log_error.xtf");
        await DeleteIfExistsAsync(xtfLogFile);

        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
        };

        var result = await ili2GpkgClient.ValidateAsync(args, inputFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Log);
        Assert.IsNotEmpty(result.Log);
        Assert.IsFalse(result.Success, "Validation should have failed. Log: " + result.Log);
        Assert.MatchesRegex(MandatoryConstraintErrorRegex(), result.Log);

        await AssertIsInterlisTransferAsync(xtfLogFile, InterlisVersion.Ili2_3);
    }

    private async Task DeleteIfExistsAsync(PipelineFile file)
    {
        var path = await file.GetLocalPathAsync();
        if (File.Exists(path))
        {
            File.Delete(path);
        }
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
