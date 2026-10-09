using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Processes.XtfDiff;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.RegularExpressions;

namespace Geopilot.Pipeline.Test.Ilitools;

/// <summary>
/// Integration tests for the <see cref="XtfDiffClient"/> class, testing against an ilitools-wrapper service running
/// locally. The transfer files belong to the repository the compose file offers as <c>xtf-error-visualization</c>.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class XtfDiffClientIntegrationTest
{
    private const string ModelRepository = "%REPOSITORIES/xtf-error-visualization";

    public TestContext TestContext { get; set; }

    private GrpcChannel grpcChannel;
    private XtfDiffClient xtfDiffClient;
    private string outputDirectory;

    [TestInitialize]
    public void SetUp()
    {
        grpcChannel = GrpcChannel.ForAddress("http://localhost:5555");
        xtfDiffClient = new XtfDiffClient(grpcChannel, NullLogger<XtfDiffClient>.Instance);
        outputDirectory = Directory.CreateTempSubdirectory("XtfDiffClient_").FullName;
    }

    [TestCleanup]
    public void Cleanup()
    {
        grpcChannel?.Dispose();
        Directory.Delete(outputDirectory, true);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task DiffAsyncReportsTheChangesFromTheOldToTheNewState()
    {
        var result = await DiffAsync("AllErrors24-ok.xtf", "AllErrors24-errors.xtf");

        Assert.IsTrue(result.Compared, "The two states should have been compared. Log:\n" + result.Log);
        Assert.Contains("AllErrors24", result.Log, "The log should name the model the tool compiled.");

        // The object only the old state contains is deleted. The tool writes the properties of a change in a fixed order.
        var deletion = new Regex("\"oid\"\\s*:\\s*\"550e8400-e29b-41d4-a716-446655440010\"\\s*,\\s*\"changeType\"\\s*:\\s*\"deleted\"");
        Assert.IsTrue(deletion.IsMatch(result.Diff), "The diff should report the object of the old state as deleted. Diff:\n" + result.Diff);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task DiffAsyncNamesDifferentInterlisVersionsInTheLog()
    {
        var result = await DiffAsync("AllErrors23-ok.xtf", "AllErrors24-ok.xtf");

        Assert.IsFalse(result.Compared, "States of different INTERLIS versions cannot be compared.");
        Assert.Contains(XtfDiffProcess.DifferentInterlisVersionsMarker, result.Log, "The process recognizes the cause by this line.");
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task DiffAsyncNamesDifferentModelsInTheLog()
    {
        var result = await DiffAsync("AllErrors24-ok.xtf", "Grundwasservorkommen_24.xtf");

        Assert.IsFalse(result.Compared, "States of different models cannot be compared.");
        Assert.Contains(XtfDiffProcess.DifferentModelsMarker, result.Log, "The process recognizes the cause by this line.");
    }

    private async Task<(bool Compared, string Log, string Diff)> DiffAsync(string oldFileName, string newFileName)
    {
        var oldFile = new PipelineFile(Path.Combine("TestData", "ModelRepository", oldFileName), oldFileName);
        var newFile = new PipelineFile(Path.Combine("TestData", "ModelRepository", newFileName), newFileName);
        var logPath = Path.Combine(outputDirectory, "diff.log");
        var diffPath = Path.Combine(outputDirectory, "diff.json");

        var compared = await xtfDiffClient.DiffAsync(
            [ModelRepository], oldFile, newFile, new PipelineFile(logPath, "diff.log"), new PipelineFile(diffPath, "diff.json"), TestContext.CancellationToken);

        // Without a diff the client never creates the file.
        var log = await ReadIfExistsAsync(logPath);
        var diff = await ReadIfExistsAsync(diffPath);
        return (compared, log, diff);
    }

    private async Task<string> ReadIfExistsAsync(string path)
    {
        return File.Exists(path) ? await File.ReadAllTextAsync(path, TestContext.CancellationToken) : string.Empty;
    }
}
