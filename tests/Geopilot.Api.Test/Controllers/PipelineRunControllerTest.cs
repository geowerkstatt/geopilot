using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Controllers;

[TestClass]
public sealed class PipelineRunControllerTest
{
    // The list tests seed their runs into a window of their own, so nothing else in the database counts.
    private static readonly DateTime Window = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private Context context;
    private Mock<IProcessingService> processingServiceMock;
    private PipelineRunController controller;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        processingServiceMock = new Mock<IProcessingService>();
        controller = new PipelineRunController(Mock.Of<ILogger<PipelineRunController>>(), context, processingServiceMock.Object);
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

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(run.JobId, CancellationToken.None));

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

        CollectionAssert.AreEqual(
            new[]
            {
                new PipelineRunInputResponse("transferFile", InputOrigin.StepOutput, "matching.XtfFiles", InputKind.File, null, "data.xtf", 1024, null, true),
                new PipelineRunInputResponse("profile", InputOrigin.Definition, null, InputKind.Value, "DMAV", null, null, null, null),
            },
            response.Steps[1].Inputs,
            "the inputs are returned in the order they were recorded.");
    }

    [TestMethod]
    public async Task GetReturnsTheReactionEventsInTheOrderTheyHappened()
    {
        var run = SeedRun();

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(run.JobId, CancellationToken.None));

        CollectionAssert.AreEqual(new int?[] { 1, 2 }, response.ReactionEvents.Select(reactionEvent => reactionEvent.Attempt).ToArray());
        var first = response.ReactionEvents[0];
        Assert.AreEqual(42, first.DeliveryId);
        Assert.AreEqual("weitergabe", first.ReactionId);
        Assert.AreEqual(ReactionKind.Transfer, first.ReactionKind);
        Assert.AreEqual(ReactionEvent.Attempt, first.Event);
        Assert.AreEqual(ReactionState.Pending, first.State);
        Assert.AreEqual(TestHelpers.Localized("The target is not reachable."), first.Message);
        Assert.IsNotNull(first.FinishedAt);
        Assert.IsNull(first.UserId);
    }

    [TestMethod]
    public async Task GetReturnsWhetherTheRunWasDeclarableAndWhyItsDeclarationFailed()
    {
        var run = SeedRun();
        run.TerminalState = ProcessingState.Success;
        run.Declarability = Declarability.Declarable;
        run.DeclarationFailure = "No assets found.";
        context.SaveChanges();

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(run.JobId, CancellationToken.None));

        Assert.AreEqual(Declarability.Declarable, response.Declarability);
        Assert.AreEqual("No assets found.", response.DeclarationFailure);
    }

    [TestMethod]
    public async Task GetLeavesTheDeclarabilityOfAnOlderRunOpen()
    {
        var run = SeedRun();

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(run.JobId, CancellationToken.None));

        Assert.IsNull(response.Declarability, "A run recorded before the protocol kept its declarability says nothing about it, which is not the same as not declarable.");
    }

    [TestMethod]
    public async Task GetReturnsNotFoundForUnknownJob()
    {
        ActionResultAssert.IsNotFound(await controller.Get(Guid.NewGuid(), CancellationToken.None));
    }

    [TestMethod]
    public async Task GetNamesDeclarerAndMandate()
    {
        var user = context.Users.First();
        var mandate = context.Mandates.First();
        var run = SeedRun(r =>
        {
            r.UserId = user.Id;
            r.MandateId = mandate.Id;
        });

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(run.JobId, CancellationToken.None));

        Assert.IsNotNull(response.Declarer);
        Assert.AreEqual(DeclarerKind.Person, response.Declarer.Kind);
        Assert.AreEqual(user.Id, response.Declarer.Id);
        Assert.AreEqual(user.FullName, response.Declarer.Name);
        Assert.IsNotNull(response.Mandate);
        Assert.AreEqual(mandate.Id, response.Mandate.Id);
    }

    [TestMethod]
    public async Task GetNamesTheClientReference()
    {
        var run = SeedRun(r => r.ClientReference = "AV-2026-0042");

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(run.JobId, CancellationToken.None));

        Assert.AreEqual("AV-2026-0042", response.ClientReference);
    }

    [TestMethod]
    public async Task GetReportsTheDeliveryAsOutcome()
    {
        var run = SeedRun();
        var delivery = SeedDelivery(run.JobId, deleted: false);

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(run.JobId, CancellationToken.None));

        Assert.AreEqual(PipelineRunOutcomeKind.Delivered, response.Outcome.Kind);
        Assert.AreEqual(delivery.Id, response.Outcome.DeliveryId);
    }

    [TestMethod]
    public async Task GetSumsUpTheReactionsOfTheLiveDeliveryOnly()
    {
        var run = SeedRun();
        var deleted = SeedDelivery(run.JobId, deleted: true);
        var live = SeedDelivery(run.JobId, deleted: false);
        context.DeliveryReactions.AddRange(
            Reaction(deleted, ReactionKind.Transfer, ReactionState.Error),
            Reaction(live, ReactionKind.Transfer, ReactionState.Pending),
            Reaction(live, ReactionKind.Transfer, ReactionState.Done),
            Reaction(live, ReactionKind.Notification, ReactionState.Error));
        context.SaveChanges();

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(run.JobId, CancellationToken.None));

        Assert.AreEqual(ReactionState.Pending, response.ForwardingState);
        Assert.AreEqual(ReactionState.Error, response.NotificationState);
    }

    [TestMethod]
    public async Task GetPrefersTheLiveDeliveryOverADeletedOne()
    {
        var run = SeedRun();
        SeedDelivery(run.JobId, deleted: true);
        var live = SeedDelivery(run.JobId, deleted: false);

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(run.JobId, CancellationToken.None));

        Assert.AreEqual(PipelineRunOutcomeKind.Delivered, response.Outcome.Kind);
        Assert.AreEqual(live.Id, response.Outcome.DeliveryId);
    }

    [TestMethod]
    public async Task GetReportsAnUnfinishedRunAsRunningWhileItsJobIsActive()
    {
        var run = SeedRun(r =>
        {
            r.TerminalState = null;
            r.TerminalAt = null;
        });
        processingServiceMock
            .Setup(service => service.GetJob(run.JobId))
            .Returns(new ProcessingJob(run.JobId, run.UploadId, null, DateTime.UtcNow) { State = ProcessingState.Running });

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(run.JobId, CancellationToken.None));

        Assert.AreEqual(PipelineRunOutcomeKind.Running, response.Outcome.Kind);
    }

    [TestMethod]
    public async Task GetDefinitionServesTheSnapshotVerbatim()
    {
        var run = SeedRun();

        var result = await controller.GetDefinition(run.JobId, CancellationToken.None);

        var content = result as ContentResult;
        Assert.IsNotNull(content, "the snapshot is passed through verbatim, not re-serialized.");
        Assert.AreEqual("application/json", content.ContentType);
        Assert.AreEqual("""{"pipelines": []}""", content.Content);
    }

    [TestMethod]
    public async Task GetDefinitionReturnsNotFoundForUnknownJob()
    {
        ActionResultAssert.IsNotFound(await controller.GetDefinition(Guid.NewGuid(), CancellationToken.None));
    }

    [TestMethod]
    public async Task ListFiltersByMandatesDeclarersAndStates()
    {
        var mandates = context.Mandates.OrderBy(m => m.Id).Take(2).ToList();
        var user = context.Users.First();
        var client = new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "Test client" };
        context.MachineClients.Add(client);
        context.SaveChanges();

        SeedRun(r =>
        {
            r.StartedAt = Window.AddMinutes(1);
            r.UserId = user.Id;
            r.MandateId = mandates[0].Id;
            r.TerminalState = ProcessingState.Success;
        });
        SeedRun(r =>
        {
            r.StartedAt = Window.AddMinutes(2);
            r.MachineClientId = client.Id;
            r.MandateId = mandates[0].Id;
            r.TerminalState = ProcessingState.Failed;
        });
        SeedRun(r =>
        {
            r.StartedAt = Window.AddMinutes(3);
            r.MandateId = mandates[1].Id;
            r.TerminalState = ProcessingState.Warning;
        });
        var open = SeedRun(r =>
        {
            r.StartedAt = Window.AddMinutes(4);
            r.MandateId = mandates[1].Id;
            r.TerminalState = null;
            r.TerminalAt = null;
        });

        var byPersonOrClient = InWindow(q =>
        {
            q.UserIds = [user.Id];
            q.MachineClientIds = [client.Id];
        });
        var failedOfFirstMandate = InWindow(q =>
        {
            q.MandateIds = [mandates[0].Id];
            q.States = [PipelineRunStateFilter.Failed, PipelineRunStateFilter.Warning];
        });

        Assert.AreEqual(2, (await List(InWindow(q => q.MandateIds = [mandates[0].Id]))).TotalCount);
        Assert.AreEqual(4, (await List(InWindow(q => q.MandateIds = [mandates[0].Id, mandates[1].Id]))).TotalCount);
        Assert.AreEqual(1, (await List(InWindow(q => q.UserIds = [user.Id]))).TotalCount);
        Assert.AreEqual(1, (await List(InWindow(q => q.MachineClientIds = [client.Id]))).TotalCount);
        Assert.AreEqual(2, (await List(byPersonOrClient)).TotalCount, "Persons and machine clients add up.");
        Assert.AreEqual(1, (await List(InWindow(q => q.States = [PipelineRunStateFilter.Failed]))).TotalCount);
        Assert.AreEqual(open.JobId, (await List(InWindow(q => q.States = [PipelineRunStateFilter.Open]))).Items.Single().Latest.JobId);
        Assert.AreEqual(2, (await List(InWindow(q => q.States = [PipelineRunStateFilter.Success, PipelineRunStateFilter.Open]))).TotalCount);
        Assert.AreEqual(1, (await List(failedOfFirstMandate)).TotalCount, "Different filters narrow each other.");
    }

    [TestMethod]
    public async Task ListPagesNewestFirstWithTotalCount()
    {
        var runs = Enumerable.Range(0, 5).Select(i => SeedRun(r => r.StartedAt = Window.AddMinutes(i))).ToList();

        var page = await List(InWindow(q =>
        {
            q.PageSize = 2;
            q.Page = 1;
        }));
        Assert.AreEqual(5, page.TotalCount);
        CollectionAssert.AreEqual(new[] { runs[2].JobId, runs[1].JobId }, page.Items.Select(item => item.Latest.JobId).ToArray(), "newest first, so the second page holds the third and fourth newest.");

        var beyond = await List(InWindow(q =>
        {
            q.PageSize = 2;
            q.Page = 9;
        }));
        Assert.AreEqual(5, beyond.TotalCount);
        Assert.HasCount(0, beyond.Items);
    }

    [TestMethod]
    public async Task ListCapsThePageSize()
    {
        SeedMinimalRuns(101);

        Assert.HasCount(100, (await List(InWindow(q => q.PageSize = 1000))).Items);
        Assert.HasCount(1, (await List(InWindow(q => q.PageSize = 0))).Items);
    }

    [TestMethod]
    public async Task ListFiltersByTimeRangeWithExclusiveEnd()
    {
        var atFrom = SeedRun(r => r.StartedAt = Window);
        SeedRun(r => r.StartedAt = Window.AddHours(1));

        var page = await List(new PipelineRunQuery { From = Window, To = Window.AddHours(1) });

        Assert.AreEqual(atFrom.JobId, page.Items.Single().Latest.JobId);
    }

    [TestMethod]
    public async Task ListReadsTimesWithoutOffsetAsUtc()
    {
        var run = SeedRun(r => r.StartedAt = Window.AddMinutes(30));

        // A query string time without offset binds as unspecified, and the API documents its times as UTC.
        var page = await List(new PipelineRunQuery
        {
            From = DateTime.SpecifyKind(Window, DateTimeKind.Unspecified),
            To = DateTime.SpecifyKind(Window.AddHours(1), DateTimeKind.Unspecified),
        });

        Assert.AreEqual(run.JobId, page.Items.Single().Latest.JobId);
    }

    [TestMethod]
    public async Task ListAnswersAnEmptyPageForAPageNumberBeyondAnyOffset()
    {
        SeedRun(r => r.StartedAt = Window);

        var page = await List(InWindow(q =>
        {
            q.Page = 21474837;
            q.PageSize = 100;
        }));

        Assert.AreEqual(1, page.TotalCount);
        Assert.HasCount(0, page.Items);
    }

    [TestMethod]
    public async Task ListShowsAnonymousRunsWithoutMandate()
    {
        SeedRun(r =>
        {
            r.StartedAt = Window;
            r.TerminalState = ProcessingState.Success;
        });

        var item = (await List(InWindow())).Items.Single().Latest;

        Assert.IsNull(item.Declarer);
        Assert.IsNull(item.Mandate);
        Assert.AreEqual(PipelineRunOutcomeKind.NotDeclarable, item.Outcome.Kind);
    }

    [TestMethod]
    public async Task ListRejectsFromAfterTo()
    {
        ActionResultAssert.IsBadRequest(await controller.List(new PipelineRunQuery { From = Window.AddDays(1), To = Window }, CancellationToken.None));
    }

    [TestMethod]
    public async Task ListGroupsTheAttemptsOfOneDeliveryUnderTheNewest()
    {
        var client = AddMachineClient();
        var mandate = context.Mandates.First();
        SeedAttempt(Window.AddMinutes(1), "AV-1", mandate.Id, client.Id);
        SeedAttempt(Window.AddMinutes(2), "AV-1", mandate.Id, client.Id);
        var third = SeedAttempt(Window.AddMinutes(3), "AV-1", mandate.Id, client.Id);

        var page = await List(InWindow());

        Assert.AreEqual(1, page.TotalCount, "The attempts of one delivery are one row.");
        var group = page.Items.Single();
        Assert.AreEqual(third.JobId, group.Latest.JobId);
        Assert.AreEqual("AV-1", group.Latest.ClientReference);
        Assert.AreEqual(3, group.AttemptCount);
    }

    [TestMethod]
    public async Task GetListsAllAttemptsOfTheDeliveryNewestFirst()
    {
        var client = AddMachineClient();
        var mandates = context.Mandates.OrderBy(m => m.Id).Take(2).ToList();
        var first = SeedAttempt(Window.AddMinutes(1), "AV-1", mandates[0].Id, client.Id);
        var second = SeedAttempt(Window.AddMinutes(2), "AV-1", mandates[0].Id, client.Id);
        var third = SeedAttempt(Window.AddMinutes(3), "AV-1", mandates[0].Id, client.Id);
        SeedAttempt(Window.AddMinutes(4), "AV-1", mandates[1].Id, client.Id);
        SeedAttempt(Window.AddMinutes(5), "av-1", mandates[0].Id, client.Id);

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(second.JobId, CancellationToken.None));

        CollectionAssert.AreEqual(
            new[] { third.JobId, second.JobId, first.JobId },
            response.Attempts.Select(a => a.JobId).ToArray(),
            "An earlier attempt lists the later ones too; another mandate or another spelling is another delivery.");
    }

    [TestMethod]
    public async Task GetListsARunWithoutReferenceAsItsOnlyAttempt()
    {
        var client = AddMachineClient();
        var mandate = context.Mandates.First();
        var run = SeedAttempt(Window.AddMinutes(1), null, mandate.Id, client.Id);
        SeedAttempt(Window.AddMinutes(2), null, mandate.Id, client.Id);

        var response = ActionResultAssert.IsOkObjectResult<PipelineRunResponse>(await controller.Get(run.JobId, CancellationToken.None));

        Assert.AreEqual(run.JobId, response.Attempts.Single().JobId);
    }

    [TestMethod]
    public async Task ListKeepsTheAttemptsOfOtherSendersMandatesAndReferencesApart()
    {
        var mandates = context.Mandates.OrderBy(m => m.Id).Take(2).ToList();
        var client = AddMachineClient();
        var otherClient = AddMachineClient();
        var user = context.Users.First();
        SeedAttempt(Window.AddMinutes(1), "AV-1", mandates[0].Id, client.Id);
        SeedAttempt(Window.AddMinutes(2), "AV-1", mandates[0].Id, otherClient.Id);
        SeedAttempt(Window.AddMinutes(3), "AV-1", mandates[0].Id, userId: user.Id);
        SeedAttempt(Window.AddMinutes(4), "AV-1", mandates[1].Id, client.Id);
        SeedAttempt(Window.AddMinutes(5), "av-1", mandates[0].Id, client.Id);

        var page = await List(InWindow());

        Assert.AreEqual(5, page.TotalCount, "Another client, a person, another mandate or another spelling is another delivery.");
        Assert.IsTrue(page.Items.All(group => group.AttemptCount == 1));
    }

    [TestMethod]
    public async Task ListKeepsRunsWithoutClientReferenceApart()
    {
        var client = AddMachineClient();
        var mandate = context.Mandates.First();
        SeedAttempt(Window.AddMinutes(1), null, mandate.Id, client.Id);
        SeedAttempt(Window.AddMinutes(2), null, mandate.Id, client.Id);

        Assert.AreEqual(2, (await List(InWindow())).TotalCount, "Without a reference nothing says that two attempts belong together.");
    }

    [TestMethod]
    public async Task ListShowsAGroupWhenOneOfItsAttemptsLiesInThePeriod()
    {
        var client = AddMachineClient();
        var mandate = context.Mandates.First();
        SeedAttempt(Window.AddMinutes(1), "AV-1", mandate.Id, client.Id);
        var newestOutside = SeedAttempt(Window.AddDays(2), "AV-1", mandate.Id, client.Id);
        SeedAttempt(Window.AddDays(3), "AV-2", mandate.Id, client.Id);

        var page = await List(InWindow());

        Assert.AreEqual(1, page.TotalCount, "A delivery shows when one of its attempts lies in the period; the other one lies wholly outside.");
        var group = page.Items.Single();
        Assert.AreEqual(newestOutside.JobId, group.Latest.JobId, "The row shows the newest attempt, even one after the period.");
        Assert.AreEqual(2, group.AttemptCount, "The attempt in the period counts as well.");
    }

    [TestMethod]
    public async Task ListFiltersTheStateOnTheNewestAttempt()
    {
        var client = AddMachineClient();
        var mandate = context.Mandates.First();
        SeedAttempt(Window.AddMinutes(1), "AV-1", mandate.Id, client.Id, configure: r => r.TerminalState = ProcessingState.Failed);
        SeedAttempt(Window.AddMinutes(2), "AV-1", mandate.Id, client.Id, configure: r => r.TerminalState = ProcessingState.Success);
        SeedAttempt(Window.AddMinutes(3), "AV-2", mandate.Id, client.Id, configure: r => r.TerminalState = ProcessingState.Success);
        var openNewest = SeedAttempt(Window.AddMinutes(4), "AV-2", mandate.Id, client.Id, configure: r =>
        {
            r.TerminalState = null;
            r.TerminalAt = null;
        });

        Assert.AreEqual(0, (await List(InWindow(q => q.States = [PipelineRunStateFilter.Failed]))).TotalCount, "A delivery that went through in its second attempt no longer fails.");
        Assert.AreEqual(2, (await List(InWindow(q => q.States = [PipelineRunStateFilter.Success]))).Items.Single().AttemptCount, "The newest attempt decides; the failed one still counts.");
        Assert.AreEqual(openNewest.JobId, (await List(InWindow(q => q.States = [PipelineRunStateFilter.Open]))).Items.Single().Latest.JobId);
    }

    [TestMethod]
    public async Task ListTakesTheHighestIdAsTheNewestAttempt()
    {
        // The protocol writes a row when a job starts, so the higher id is the later attempt, also where two hosts
        // disagree on the time.
        var client = AddMachineClient();
        var mandate = context.Mandates.First();
        SeedAttempt(Window.AddMinutes(3), "AV-1", mandate.Id, client.Id);
        var second = SeedAttempt(Window.AddMinutes(1), "AV-1", mandate.Id, client.Id);

        var group = (await List(InWindow())).Items.Single();

        Assert.AreEqual(second.JobId, group.Latest.JobId);
    }

    [TestMethod]
    public async Task ListGroupsWithoutAPeriod()
    {
        // The default view sends no period; a client of its own keeps the other runs of the database out.
        var client = AddMachineClient();
        var mandate = context.Mandates.First();
        SeedAttempt(Window.AddMinutes(1), "AV-1", mandate.Id, client.Id);
        var newest = SeedAttempt(Window.AddMinutes(2), "AV-1", mandate.Id, client.Id);
        SeedAttempt(Window.AddMinutes(3), null, mandate.Id, client.Id);

        var page = await List(new PipelineRunQuery { MachineClientIds = [client.Id] });

        Assert.AreEqual(2, page.TotalCount);
        Assert.AreEqual(newest.JobId, page.Items.Single(group => group.AttemptCount == 2).Latest.JobId);
    }

    [TestMethod]
    public async Task ListPagesGroupsAsOneRow()
    {
        var client = AddMachineClient();
        var mandate = context.Mandates.First();
        SeedAttempt(Window.AddMinutes(1), null, mandate.Id, client.Id);
        SeedAttempt(Window.AddMinutes(2), null, mandate.Id, client.Id);
        SeedAttempt(Window.AddMinutes(3), "AV-1", mandate.Id, client.Id);
        SeedAttempt(Window.AddMinutes(4), "AV-1", mandate.Id, client.Id);
        var groupNewest = SeedAttempt(Window.AddMinutes(5), "AV-1", mandate.Id, client.Id);

        var page = await List(InWindow(q => q.PageSize = 2));

        Assert.AreEqual(3, page.TotalCount, "Three attempts of one delivery and two single runs make three rows.");
        Assert.HasCount(2, page.Items);
        Assert.AreEqual(groupNewest.JobId, page.Items[0].Latest.JobId);
    }

    private static PipelineRunQuery InWindow(Action<PipelineRunQuery>? configure = null)
    {
        var query = new PipelineRunQuery { From = Window, To = Window.AddDays(1) };
        configure?.Invoke(query);
        return query;
    }

    private async Task<PipelineRunPageResponse> List(PipelineRunQuery query)
        => ActionResultAssert.IsOkObjectResult<PipelineRunPageResponse>(await controller.List(query, CancellationToken.None));

    private MachineClient AddMachineClient()
    {
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "Test client" }).Entity;
        context.SaveChanges();
        return client;
    }

    // An attempt of a delivery, started by the machine client or by the user given.
    private PipelineRun SeedAttempt(DateTime startedAt, string? clientReference, int mandateId, int? machineClientId = null, int? userId = null, Action<PipelineRun>? configure = null)
        => SeedRun(r =>
        {
            r.StartedAt = startedAt;
            r.ClientReference = clientReference;
            r.MandateId = mandateId;
            r.MachineClientId = machineClientId;
            r.UserId = userId;
            configure?.Invoke(r);
        });

    private void SeedMinimalRuns(int count)
    {
        context.PipelineRuns.AddRange(Enumerable.Range(0, count).Select(i => new PipelineRun
        {
            JobId = Guid.NewGuid(),
            PipelineId = "pipe_a",
            Definition = "{}",
            AppVersion = "3.0-test",
            ClientKind = ClientKind.ApiClient,
            UploadId = Guid.NewGuid(),
            UploadStorageLocation = "https://storage.example.com/uploads",
            UploadInitiatedAt = Window,
            StartedAt = Window.AddSeconds(i),
            ScanState = ScanState.Clean,
        }));
        context.SaveChanges();
    }

    private Delivery SeedDelivery(Guid jobId, bool deleted)
    {
        var delivery = new Delivery { JobId = jobId, Mandate = context.Mandates.First(), DeclaringUser = context.Users.First(), Deleted = deleted };
        context.Deliveries.Add(delivery);
        context.SaveChanges();
        return delivery;
    }

    private static DeliveryReaction Reaction(Delivery delivery, ReactionKind kind, ReactionState state) => new()
    {
        Delivery = delivery,
        ReactionId = $"{kind}-{state}",
        Kind = kind,
        Implementation = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel",
        State = state,
        NextAttemptAt = DateTime.UtcNow,
    };

    private PipelineRun SeedRun(Action<PipelineRun>? configure = null)
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
                    Inputs = new List<PipelineRunInput>
                    {
                        new() { Parameter = "transferFile", Origin = InputOrigin.StepOutput, Reference = "matching.XtfFiles", Kind = InputKind.File, FileName = "data.xtf", Size = 1024, FromUpload = true },
                        new() { Parameter = "profile", Origin = InputOrigin.Definition, Kind = InputKind.Value, Value = "DMAV" },
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
            ReactionEvents = new List<PipelineRunReactionEvent>
            {
                // Inserted out of order on purpose; the response must order by when it happened.
                ReactionAttempt(2, DateTime.UtcNow.AddMinutes(-1), ReactionState.Done, "Delivered to the target."),
                ReactionAttempt(1, DateTime.UtcNow.AddMinutes(-3), ReactionState.Pending, "The target is not reachable."),
            },
        };
        configure?.Invoke(run);

        context.PipelineRuns.Add(run);
        context.SaveChanges();
        return run;
    }

    private static PipelineRunReactionEvent ReactionAttempt(int attempt, DateTime startedAt, ReactionState state, string message) => new()
    {
        DeliveryId = 42,
        ReactionId = "weitergabe",
        ReactionKind = ReactionKind.Transfer,
        Implementation = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel",
        Event = ReactionEvent.Attempt,
        Attempt = attempt,
        OccurredAt = startedAt,
        FinishedAt = startedAt.AddSeconds(5),
        State = state,
        Message = TestHelpers.Localized(message),
    };
}
