using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Pipeline;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Controllers;

[TestClass]
public sealed class PipelineRunControllerTest
{
    private Context context;
    private PipelineRunController controller;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        controller = new PipelineRunController(Mock.Of<ILogger<PipelineRunController>>(), context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        context.Dispose();
    }

    [TestMethod]
    public async Task GetReturnsProtocolWithStepsInPipelineOrder()
    {
        var run = SeedRun();

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(run.JobId));

        Assert.AreEqual(run.JobId, response.JobId);
        Assert.AreEqual("pipe_a", response.PipelineId);
        Assert.AreEqual(ProcessingState.DeliveryRestriction, response.TerminalState);
        Assert.AreEqual(ScanState.Clean, response.ScanState);

        Assert.HasCount(1, response.Files);
        Assert.AreEqual("cafe01", response.Files[0].Sha256);

        Assert.HasCount(2, response.Steps, "steps are returned in pipeline order.");
        Assert.AreEqual("matching", response.Steps[0].StepId);
        Assert.AreEqual("validation", response.Steps[1].StepId);

        var condition = response.Steps[1].Conditions.Single();
        Assert.AreEqual("validation-failed", condition.ConditionId);
        Assert.AreEqual(ConditionKind.RestrictDelivery, condition.Kind);
        Assert.IsTrue(condition.Matched);
        Assert.IsNotNull(condition.EvaluatedValues);
        Assert.AreEqual("False", condition.EvaluatedValues["validation.ValidationSuccessful"], "the stored evaluation values must reach the response as a map.");

        var artifact = response.Steps[1].Artifacts.Single();
        Assert.AreEqual(ArtifactKind.Download, artifact.Kind);
        Assert.AreEqual("error.log", artifact.OriginalFileName);
    }

    [TestMethod]
    public async Task GetReturnsNotFoundForUnknownJob()
    {
        ActionResultAssert.IsNotFound(await controller.Get(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task GetDefinitionServesTheSnapshotVerbatim()
    {
        var run = SeedRun();

        var result = await controller.GetDefinition(run.JobId);

        var content = result as ContentResult;
        Assert.IsNotNull(content, "the snapshot is passed through verbatim, not re-serialized.");
        Assert.AreEqual("application/json", content.ContentType);
        Assert.AreEqual("""{"pipelines": []}""", content.Content);
    }

    [TestMethod]
    public async Task GetDefinitionReturnsNotFoundForUnknownJob()
    {
        ActionResultAssert.IsNotFound(await controller.GetDefinition(Guid.NewGuid()));
    }

    private PipelineRun SeedRun()
    {
        var run = new PipelineRun
        {
            JobId = Guid.NewGuid(),
            PipelineId = "pipe_a",
            Definition = """{"pipelines": []}""",
            AppVersion = "3.0-test",
            ClientKind = ClientKind.ApiClient,
            UploadId = Guid.NewGuid(),
            UploadStorageLocation = "https://storage.example.com/uploads",
            UploadInitiatedAt = DateTime.UtcNow.AddMinutes(-10),
            StartedAt = DateTime.UtcNow.AddMinutes(-9),
            ScanState = ScanState.Clean,
            TerminalState = ProcessingState.DeliveryRestriction,
            TerminalAt = DateTime.UtcNow,
            Files = new List<PipelineRunFile>
            {
                new() { FileName = "data.xtf", StorageKey = "uploads/1/data.xtf", DeclaredSize = 1024, Sha256 = "cafe01" },
            },
            Steps = new List<PipelineRunStep>
            {
                // Inserted out of pipeline order on purpose; the response must order by Order.
                new()
                {
                    Order = 1,
                    StepId = "validation",
                    DisplayName = TestHelpers.Localized("Validierung"),
                    ProcessImplementation = "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess",
                    State = StepState.DeliveryRestriction,
                    Conditions = new List<PipelineRunCondition>
                    {
                        new()
                        {
                            ConditionId = "validation-failed",
                            Phase = ConditionPhase.Post,
                            Kind = ConditionKind.RestrictDelivery,
                            Expression = "!([validation.ValidationSuccessful])",
                            Matched = true,
                            EvaluatedValues = """{"validation.ValidationSuccessful":"False"}""",
                        },
                    },
                    Artifacts = new List<PipelineRunArtifact>
                    {
                        new() { Kind = ArtifactKind.Download, OriginalFileName = "error.log", PersistedFileName = "validation_error.log" },
                    },
                },
                new()
                {
                    Order = 0,
                    StepId = "matching",
                    DisplayName = TestHelpers.Localized("Zuordnung"),
                    ProcessImplementation = "Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess",
                    State = StepState.Success,
                },
            },
        };

        context.PipelineRuns.Add(run);
        context.SaveChanges();
        return run;
    }
}
