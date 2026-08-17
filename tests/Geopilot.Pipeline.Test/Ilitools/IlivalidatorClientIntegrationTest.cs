using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Ilitools;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging.Abstractions;

namespace Geopilot.Pipeline.Test.Ilitools;

/// <summary>
/// Integration tests for the <see cref="IlivalidatorClient"/> class, testing against an ilitools-wrapper service
/// running locally.
/// </summary>
/// <remarks>
/// A successful validation is not covered here, because it needs a model the tool can resolve: the test data
/// references the local model SimpleModel, the client cannot send a model file (the validate contract of the wrapper
/// has no file type for it, see geopilot#683), and a published model would make the suite depend on
/// models.interlis.ch. Serving a repository to the wrapper instead is no way around it either, since the container
/// would have to reach the test host through a private address, which the wrapper rejects by default. What these
/// tests cover is the round trip including both log files, and that the model repository options reach the wrapper.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
public class IlivalidatorClientIntegrationTest
{
    public TestContext TestContext { get; set; }

    private GrpcChannel grpcChannel;
    private IlivalidatorClient ilivalidatorClient;

    [TestInitialize]
    public void SetUp()
    {
        grpcChannel = GrpcChannel.ForAddress("http://localhost:5555");
        ilivalidatorClient = new IlivalidatorClient(grpcChannel, NullLogger<IlivalidatorClient>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        grpcChannel?.Dispose();
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncReportsUnresolvableModel()
    {
        var transferFile = GetTestPipelineFile("transfer.xtf");
        var logFile = GetTestPipelineFile("validation.log");
        var xtfLogFile = GetTestPipelineFile("validation_log.xtf");
        await DeleteIfExistsAsync(logFile);
        await DeleteIfExistsAsync(xtfLogFile);

        var result = await ilivalidatorClient.ValidateAsync(new IlivalidatorArgs(), transferFile, logFile, xtfLogFile, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Success, "Validation should fail because the model of the transfer file is not reachable.");

        // Both logs have to arrive in their own file, so the assertions also pin that the two streams are not mixed.
        var log = await File.ReadAllTextAsync(await logFile.GetLocalPathAsync(), TestContext.CancellationToken);
        Assert.Contains("SimpleModel", log, "The tool log should name the model it could not resolve.");
        Assert.DoesNotContain("<?xml", log, "The tool log must not carry the content of the XTF log.");

        // The XTF log repeats every log message, so only its transfer file structure tells the two apart.
        var xtfLog = await File.ReadAllTextAsync(await xtfLogFile.GetLocalPathAsync(), TestContext.CancellationToken);
        Assert.Contains("IliVErrors", xtfLog, "The XTF log should be an INTERLIS error log.");
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncFailsWithLocalPathAsModelDir()
    {
        var transferFile = GetTestPipelineFile("transfer.xtf");
        var logFile = GetTestPipelineFile("validation_invalid_modeldir.log");
        var xtfLogFile = GetTestPipelineFile("validation_invalid_modeldir.xtf");

        // Only http(s) URLs and the tool placeholder are accepted, so the wrapper has to reject a local path.
        var args = new IlivalidatorArgs { ModelDirs = ["/etc/models"] };

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ilivalidatorClient.ValidateAsync(args, transferFile, logFile, xtfLogFile, TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncFailsWithMetaConfigFilePath()
    {
        var transferFile = GetTestPipelineFile("transfer.xtf");
        var logFile = GetTestPipelineFile("validation_invalid_metaconfig.log");
        var xtfLogFile = GetTestPipelineFile("validation_invalid_metaconfig.xtf");

        // Only the repository indexed form ilidata:<DatasetId> is accepted, not a file path.
        var args = new IlivalidatorArgs { MetaConfig = "profile.toml" };

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ilivalidatorClient.ValidateAsync(args, transferFile, logFile, xtfLogFile, TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
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
}
