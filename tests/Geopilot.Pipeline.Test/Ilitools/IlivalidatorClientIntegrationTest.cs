using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Processes.XtfValidation;
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
/// These tests use a transfer file whose model no reachable repository indexes, so they cover the error paths: the
/// round trip including both log files, the rejection of an invalid model repository option, and the marker the
/// process needs to recognize an unresolvable profile. A validation that runs to a verdict is covered by
/// <see cref="IlivalidatorRepositoryIntegrationTest"/>, against the model repository the compose file serves.
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

        // %ITF_DIR is the session directory of the wrapper, which holds nothing but the file just sent. Naming it
        // replaces the tool default, so the tool cannot reach any remote repository and gives up at once. Without
        // this the runtime depends on how long the default repositories take to answer, which timed out in CI.
        var args = new IlivalidatorArgs { ModelDirs = ["%ITF_DIR"] };

        var result = await ilivalidatorClient.ValidateAsync(args, transferFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

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
    public async Task ValidateAsyncResolvesDeliveredModel()
    {
        var transferFile = GetTestPipelineFile("transfer.xtf");
        var modelFile = GetTestPipelineFile("model.ili");
        var logFile = GetTestPipelineFile("validation_delivered_model.log");
        var xtfLogFile = GetTestPipelineFile("validation_delivered_model.xtf");
        await DeleteIfExistsAsync(logFile);
        await DeleteIfExistsAsync(xtfLogFile);

        // The delivered model lands in the models subfolder of the wrapper session, so %ITF_DIR/models is all it
        // takes to resolve it; no repository is asked.
        var args = new IlivalidatorArgs { ModelDirs = ["%ITF_DIR/models"] };

        var result = await ilivalidatorClient.ValidateAsync(args, transferFile, logFile, xtfLogFile, modelFiles: [modelFile], cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.Success, "The transfer file should validate against the delivered model.");

        var log = await File.ReadAllTextAsync(await logFile.GetLocalPathAsync(), TestContext.CancellationToken);
        Assert.Contains("validation done", log, "The tool log should report a successful validation.");
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncAppliesInterlis1SemanticsForItf()
    {
        var transferFile = GetTestPipelineFile("transfer_interlis1.itf");
        var modelFile = GetTestPipelineFile("model_interlis1.ili");
        var logFile = GetTestPipelineFile("validation_interlis1.log");
        var xtfLogFile = GetTestPipelineFile("validation_interlis1.xtf");
        await DeleteIfExistsAsync(logFile);
        await DeleteIfExistsAsync(xtfLogFile);

        var args = new IlivalidatorArgs { ModelDirs = ["%ITF_DIR/models"] };

        var result = await ilivalidatorClient.ValidateAsync(args, transferFile, logFile, xtfLogFile, modelFiles: [modelFile], cancellationToken: TestContext.CancellationToken);

        // The fixture reuses a TID across two tables, which is legal in ITF but fails under an xtf name, so this
        // only passes when the itf extension of the transfer file reaches the tool through the wrapper.
        Assert.IsTrue(result.Success, "The ITF should validate against the delivered INTERLIS 1 model.");
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncReportsAnUnresolvableProfileInTheLog()
    {
        var transferFile = GetTestPipelineFile("transfer.xtf");
        var logFile = GetTestPipelineFile("validation_unknown_profile.log");
        var xtfLogFile = GetTestPipelineFile("validation_unknown_profile.xtf");
        await DeleteIfExistsAsync(logFile);
        await DeleteIfExistsAsync(xtfLogFile);

        // Only the session directory as model dir, so the tool fails on the profile without asking a remote repository.
        var args = new IlivalidatorArgs { ModelDirs = ["%ITF_DIR"], MetaConfig = "ilidata:DOESNOTEXIST" };

        var result = await ilivalidatorClient.ValidateAsync(args, transferFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);

        Assert.IsFalse(result.Success);

        // XtfValidatorProcess turns this line into an error of its own, because the tool exits like a failed
        // validation. If a tool update rewords it, this assertion is what says so.
        var log = await File.ReadAllTextAsync(await logFile.GetLocalPathAsync(), TestContext.CancellationToken);
        Assert.Contains(XtfValidatorProcess.MetaConfigNotFoundMarker, log);
        Assert.Contains("DOESNOTEXIST", log);
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
            await ilivalidatorClient.ValidateAsync(args, transferFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);
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
            await ilivalidatorClient.ValidateAsync(args, transferFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);
        });

        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncFailsWithAnUnknownPluginId()
    {
        var transferFile = GetTestPipelineFile("transfer.xtf");
        var logFile = GetTestPipelineFile("validation_unknown_plugin.log");
        var xtfLogFile = GetTestPipelineFile("validation_unknown_plugin.xtf");

        // The wrapper offers only what its plugin directory holds, which is empty in the compose image, so every id
        // is unknown and has to be rejected. That rejection is what proves the deployed image understands the field
        // at all: proto3 drops an unknown field silently, so an image without it would validate as if no plugin had
        // been selected and report success. This test therefore guards the pairing of the stub version referenced
        // here against the wrapper image the compose file pins.
        var args = new IlivalidatorArgs { PluginIds = ["no-such-plugin"] };

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await ilivalidatorClient.ValidateAsync(args, transferFile, logFile, xtfLogFile, cancellationToken: TestContext.CancellationToken);
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
