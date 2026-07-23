using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Ilitools;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using System.Xml.Linq;

namespace Geopilot.Pipeline.Test.Ilitools;

[TestClass]
public class Ili2GpkgClientIntegrationTest
{
    public TestContext TestContext { get; set; }

    private Ili2GpkgClient ili2GpkgClient;

    [TestInitialize]
    public void SetUp()
    {
        var options = new IlitoolsOptions
        {
            IlitoolsWrapperAddress = "http://localhost:5555",
        };
        ili2GpkgClient = new Ili2GpkgClient(options, NullLogger<Ili2GpkgClient>.Instance);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task SchemaImportAsync()
    {
        var modelFile = GetTestPipelineFile("model.ili");
        var gpkgFile = GetTestPipelineFile("schema_import.gpkg");
        DeleteIfExists(gpkgFile);

        var args = new Ili2GpkgArgs();
        var result = await ili2GpkgClient.SchemaImportAsync(args, modelFile, gpkgFile, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Log);
        Assert.IsNotEmpty(result.Log);
        Assert.IsTrue(result.Success, "Schema import failed. Log: " + result.Log);

        using var stream = gpkgFile.OpenReadFileStream();
        Assert.IsGreaterThan(0, stream.Length, "Resulting GPKG file is empty.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task SchemaImportAsyncFailsWithInvalidModel()
    {
        var modelFile = GetTestPipelineFile("invalid_model.ili");
        var gpkgFile = GetTestPipelineFile("schema_import_invalid.gpkg");
        DeleteIfExists(gpkgFile);

        var args = new Ili2GpkgArgs();
        var result = await ili2GpkgClient.SchemaImportAsync(args, modelFile, gpkgFile, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Log);
        Assert.IsNotEmpty(result.Log);
        Assert.IsFalse(result.Success, "Schema import should have failed. Log: " + result.Log);

        Assert.IsFalse(File.Exists(gpkgFile.GetLocalPath()), "GPKG file should not have been created for an invalid model.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ImportAsync()
    {
        var inputFile = GetTestPipelineFile("schema.gpkg");
        var outputFile = GetTestPipelineFile("import.gpkg");
        var transferFile = GetTestPipelineFile("transfer.xtf");
        DeleteIfExists(outputFile);

        var args = new Ili2GpkgArgs();
        var result = await ili2GpkgClient.ImportAsync(args, inputFile, outputFile, [transferFile], TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Log);
        Assert.IsNotEmpty(result.Log);
        Assert.IsTrue(result.Success, "Import failed. Log: " + result.Log);

        using var inputFileStream = inputFile.OpenReadFileStream();
        using var outputFileStream = outputFile.OpenReadFileStream();
        Assert.IsGreaterThan(0, outputFileStream.Length, "Resulting GPKG file is empty.");
        Assert.IsGreaterThanOrEqualTo(inputFileStream.Length, outputFileStream.Length, "Resulting GPKG file is smaller than the input file.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ImportAsyncFailsWithoutTransferFiles()
    {
        var inputFile = GetTestPipelineFile("schema.gpkg");
        var outputFile = GetTestPipelineFile("import_invalid.gpkg");
        DeleteIfExists(outputFile);

        var args = new Ili2GpkgArgs();

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ili2GpkgClient.ImportAsync(args, inputFile, outputFile, [], TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.IsFalse(File.Exists(outputFile.GetLocalPath()), "GPKG file should not have been created for an invalid model.");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ExportAsync()
    {
        var dbFile = GetTestPipelineFile("data.gpkg");
        var transferFile = GetTestPipelineFile("export.xtf");
        DeleteIfExists(transferFile);

        var args = new Ili2GpkgArgs
        {
            Models = ["SimpleModel"],
        };
        var result = await ili2GpkgClient.ExportAsync(args, dbFile, transferFile, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Log);
        Assert.IsNotEmpty(result.Log);
        Assert.IsTrue(result.Success, "Export failed. Log: " + result.Log);

        using var stream = transferFile.OpenReadFileStream();
        Assert.IsGreaterThan(0, stream.Length, "Resulting XTF file is empty.");

        var document = await XDocument.LoadAsync(stream, LoadOptions.None, TestContext.CancellationToken);
        Assert.IsNotNull(document.Root, "Root element of the XTF file is null.");
        Assert.AreEqual("transfer", document.Root.Name.LocalName);
        Assert.AreEqual("http://www.interlis.ch/xtf/2.4/INTERLIS", document.Root.Name.NamespaceName);
    }

    private void DeleteIfExists(PipelineFile file)
    {
        if (File.Exists(file.GetLocalPath()))
        {
            File.Delete(file.GetLocalPath());
        }
    }

    private PipelineFile GetTestPipelineFile(string name)
    {
        return new PipelineFile(Path.Combine("TestData", "Ilitools", name), name);
    }
}
