using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Ilitools;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging.Abstractions;

namespace Geopilot.Pipeline.Test.Ilitools;

/// <summary>
/// Integration tests against the model repository the <c>interlis-models</c> service of the compose file serves from
/// <c>TestData/XtfErrorVisualization/ili-repository</c>. Its models are self contained and its profile is indexed in
/// its own <c>ilidata.xml</c>, so these tests need no public repository and cover what the wrapper tests cannot: a
/// validation that actually runs to a verdict, and a profile resolved from a repository.
/// </summary>
[TestClass]
[TestCategory("Integration")]

// The assembly runs test methods in parallel, and these validations resolve the same model. Measured with a cold
// ILI_CACHE, concurrent runs race while the tool materializes the model into that cache: the loser exits with the
// code of a failed validation and writes no diagnostic at all, right after the repository lookup. Roughly one run in
// four failed that way, and in CI the cache is always cold. See ilitools-wrapper#13.
[DoNotParallelize]
public class IlivalidatorRepositoryIntegrationTest
{
    private const string ModelRepository = "http://interlis-models/";
    private const string ExternalObjectsMarker = "assume unknown external objects";

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
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncSucceedsForAValidDataset()
    {
        var result = await ValidateAsync("AllErrors23-ok.xtf", "valid", new IlivalidatorArgs { ModelDirs = [ModelRepository] });

        Assert.IsTrue(result.Success, "The dataset is valid and its model is served by the repository.");
        Assert.Contains("AllErrors23", result.Log, "The tool log should name the model it resolved.");
        Assert.DoesNotContain("model(s) not found", result.Log);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncReportsTheErrorsOfTheDataset()
    {
        var result = await ValidateAsync("AllErrors23-errors.xtf", "invalid", new IlivalidatorArgs { ModelDirs = [ModelRepository] });

        Assert.IsFalse(result.Success);

        // A verdict about the data, not a failure to run: the model was resolved and the errors are the tool's own.
        Assert.DoesNotContain("model(s) not found", result.Log);
        Assert.Contains("Error:", result.Log);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncAppliesTheProfileFromTheRepository()
    {
        // The profile of the repository sets allObjectsAccessible, and the tool logs the opposite assumption when
        // nothing sets it. The pair of runs is therefore what proves the profile was resolved and took effect.
        var withoutProfile = await ValidateAsync("AllErrors23-ok.xtf", "no_profile", new IlivalidatorArgs { ModelDirs = [ModelRepository] });
        Assert.Contains(ExternalObjectsMarker, withoutProfile.Log, "Without the profile the tool assumes unknown external objects.");

        var withProfile = await ValidateAsync("AllErrors23-ok.xtf", "profile", new IlivalidatorArgs
        {
            ModelDirs = [ModelRepository],
            MetaConfig = "ilidata:DEFAULT",
        });

        Assert.IsTrue(withProfile.Success);
        Assert.Contains("ilidata:DEFAULT", withProfile.Log, "The tool log should name the meta config it used.");
        Assert.DoesNotContain(ExternalObjectsMarker, withProfile.Log, "The profile of the repository sets allObjectsAccessible.");
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncResolvesModelsFromTheArchive()
    {
        // The inline route: no published repository and no HTTP at all. The service unpacks the archive next to the
        // transfer file, which is why %ITF_DIR is the only model dir needed.
        var archive = new PipelineFile(Path.Combine("TestData", "ModelRepository", "model-repository.zip"), "model-repository.zip");
        var args = new IlivalidatorArgs { ModelDirs = ["%ITF_DIR"] };

        var result = await ValidateAsync("AllErrors23-ok.xtf", "archive", args, archive);

        Assert.IsTrue(result.Success, "The model comes from the archive, so the dataset can be validated.");
        Assert.Contains("AllErrors23", result.Log);
        Assert.DoesNotContain("model(s) not found", result.Log);
        Assert.DoesNotContain("http", result.Log, "Nothing may be resolved over the network on this route.");
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ValidateAsyncAppliesTheProfileFromTheArchive()
    {
        var archive = new PipelineFile(Path.Combine("TestData", "ModelRepository", "model-repository.zip"), "model-repository.zip");
        var args = new IlivalidatorArgs { ModelDirs = ["%ITF_DIR"], MetaConfig = "ilidata:DEFAULT" };

        var result = await ValidateAsync("AllErrors23-ok.xtf", "archive_profile", args, archive);

        Assert.IsTrue(result.Success);
        Assert.Contains("ilidata:DEFAULT", result.Log, "The profile has to be resolved out of the archive.");
        Assert.DoesNotContain(ExternalObjectsMarker, result.Log, "The profile of the repository sets allObjectsAccessible.");
    }

    private async Task<(bool Success, string Log)> ValidateAsync(string transferFileName, string outputName, IlivalidatorArgs args, PipelineFile? archive = null)
    {
        var transferFile = new PipelineFile(Path.Combine("TestData", "ModelRepository", transferFileName), transferFileName);
        var logFile = new PipelineFile(Path.Combine("TestData", "ModelRepository", $"{outputName}.log"), $"{outputName}.log");
        var xtfLogFile = new PipelineFile(Path.Combine("TestData", "ModelRepository", $"{outputName}.xtf"), $"{outputName}.xtf");
        await DeleteIfExistsAsync(logFile);
        await DeleteIfExistsAsync(xtfLogFile);

        var result = await ilivalidatorClient.ValidateAsync(args, transferFile, logFile, xtfLogFile, archive, cancellationToken: TestContext.CancellationToken);
        var log = await File.ReadAllTextAsync(await logFile.GetLocalPathAsync(), cancellationToken: TestContext.CancellationToken);

        return (result.Success, log);
    }

    private static async Task DeleteIfExistsAsync(PipelineFile file)
    {
        var path = await file.GetLocalPathAsync();
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
