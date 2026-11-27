using Geopilot.Api.Enums;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace Geopilot.Api.Test.Services;

/// <summary>
/// The delivery rules are covered through <see cref="Controllers.DeliveryControllerTest"/>, which wires the
/// service up for real but only ever creates as a user and mocks the mandate rules. What is checked here is the
/// other supplier, and who may deliver against the real mandate rules.
/// </summary>
[TestClass]
public class DeliveryCreationServiceTest
{
    private const string FileDrop = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel";

    private static readonly TimeSpan JobRetention = TimeSpan.FromHours(1);

    private Context context;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IAssetHandler> assetHandlerMock;
    private Mock<IPipelineFactory> pipelineFactoryMock;
    private DeliveryCreationService service;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        assetHandlerMock = new Mock<IAssetHandler>(MockBehavior.Strict);
        pipelineFactoryMock = new Mock<IPipelineFactory>(MockBehavior.Strict);
        service = new DeliveryCreationService(
            Mock.Of<ILogger<DeliveryCreationService>>(),
            context,
            mandateServiceMock.Object,
            assetHandlerMock.Object,
            pipelineFactoryMock.Object,
            Options.Create(new DeliveryOptions { UploaderDeleteEnabled = true, DeleteDuration = TimeSpan.FromHours(1) }),
            Options.Create(new ProcessingOptions { JobTimeout = TimeSpan.FromMinutes(5), JobRetention = JobRetention }));
    }

    [TestCleanup]
    public void Cleanup() => context.Dispose();

    [TestMethod]
    public async Task CreatesTheDeliveryForAMachineClient()
    {
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" }).Entity;
        var mandate = context.Mandates.Add(new Mandate
        {
            Name = TestHelpers.Localized(nameof(CreatesTheDeliveryForAMachineClient)),
            IsPublic = true,
            AllowDelivery = true,
            EvaluateComment = FieldEvaluationType.NotEvaluated,
            EvaluatePartial = FieldEvaluationType.NotEvaluated,
            EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
        }).Entity;
        context.SaveChanges();

        var supplier = Supplier.ForClient(client.Id);
        var jobId = SetupDeliverableJob(mandate.Id);
        mandateServiceMock.Setup(m => m.MayDeliverAsync(mandate.Id, supplier)).ReturnsAsync(true);
        SetupStagedAssets(jobId);

        var result = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), supplier, CancellationToken.None);

        Assert.AreEqual(DeliveryCreationStatus.Created, result.Status);
        var delivery = await context.Deliveries.SingleAsync(d => d.Id == result.DeliveryId);
        Assert.AreEqual(client.Id, delivery.DeliveredByClientId);
        Assert.IsNull(delivery.DeliveredByUserId, "A delivery has exactly one supplier; the database constraint says the same.");
        Assert.AreEqual("SILENTHARBOR", delivery.DeliveredByName, "The client's name is what the catalog shows as the deliverer.");
    }

    [TestMethod]
    public async Task MovesTheFilesOnlyAfterTheDeliveryIsSaved()
    {
        var (jobId, supplier) = PrepareCreation(nameof(MovesTheFilesOnlyAfterTheDeliveryIsSaved));
        var savedWhenMoved = false;
        assetHandlerMock.Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Asset> { new Asset() });
        assetHandlerMock.Setup(a => a.PromoteJobAssets(jobId)).Callback(() => savedWhenMoved = context.Deliveries.AsNoTracking().Any(d => d.JobId == jobId));

        var result = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), supplier, CancellationToken.None);

        Assert.AreEqual(DeliveryCreationStatus.Created, result.Status);
        Assert.IsTrue(savedWhenMoved, "A delivery directory may only exist together with its delivery.");
        assetHandlerMock.Verify(a => a.PromoteJobAssets(jobId), Times.Once);
    }

    [TestMethod]
    public async Task KeepsTheFilesInStagingWhenRecordingFails()
    {
        var (jobId, supplier) = PrepareCreation(nameof(KeepsTheFilesInStagingWhenRecordingFails));
        assetHandlerMock.Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("staging unreadable"));

        var result = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), supplier, CancellationToken.None);

        Assert.AreEqual(DeliveryCreationStatus.AssetRecordingFailed, result.Status);
        assetHandlerMock.Verify(a => a.PromoteJobAssets(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task KeepsTheFilesInStagingWhenSavingFails()
    {
        var (jobId, supplier) = PrepareCreation(nameof(KeepsTheFilesInStagingWhenSavingFails));
        using var aborted = new CancellationTokenSource();
        assetHandlerMock
            .Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>()))
            .Callback(() => aborted.Cancel())
            .ReturnsAsync(new List<Asset> { new Asset() });

        // The request is aborted between recording the assets and saving the delivery.
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.CreateAsync(jobId, new DeliveryFields(null, null, null), supplier, aborted.Token));

        Assert.IsFalse(context.Deliveries.AsNoTracking().Any(d => d.JobId == jobId));
        assetHandlerMock.Verify(a => a.PromoteJobAssets(It.IsAny<Guid>()), Times.Never);

        // A new request gets a new context, and the files still wait in staging for it.
        context.ChangeTracker.Clear();
        SetupStagedAssets(jobId);
        var retry = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), supplier, CancellationToken.None);

        Assert.AreEqual(DeliveryCreationStatus.Created, retry.Status);
        assetHandlerMock.Verify(a => a.PromoteJobAssets(jobId), Times.Once);
    }

    [TestMethod]
    public async Task ReportsAlreadyDeliveredWhenAnotherCallerMovedTheFilesAway()
    {
        // Both passed the check; the other caller saved its delivery and renamed staging while this one still read it.
        var (jobId, supplier) = PrepareCreation(nameof(ReportsAlreadyDeliveredWhenAnotherCallerMovedTheFilesAway));
        int? otherDeliveryId = null;
        assetHandlerMock
            .Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>()))
            .Callback(() => otherDeliveryId = SaveDeliveryOfAnotherCaller(jobId))
            .ThrowsAsync(new FileNotFoundException("moved into the delivery directory"));

        var result = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), supplier, CancellationToken.None);

        Assert.AreEqual(DeliveryCreationStatus.AlreadyDelivered, result.Status);
        Assert.AreEqual(otherDeliveryId, result.DeliveryId);
    }

    [TestMethod]
    public async Task ReportsCreatedWhenRenamingTheFilesFails()
    {
        var (jobId, supplier) = PrepareCreation(nameof(ReportsCreatedWhenRenamingTheFilesFails));
        assetHandlerMock.Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Asset> { new Asset() });
        assetHandlerMock.Setup(a => a.PromoteJobAssets(jobId)).Throws(new IOException("a file in staging is open"));

        var result = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), supplier, CancellationToken.None);

        // The delivery is saved; the next cleanup run moves its files out of staging.
        Assert.AreEqual(DeliveryCreationStatus.Created, result.Status);
        Assert.IsTrue(context.Deliveries.AsNoTracking().Any(d => d.Id == result.DeliveryId));
    }

    [TestMethod]
    public async Task LeavesTheFilesOfAnAlreadyDeliveredJobAlone()
    {
        var (jobId, supplier) = PrepareCreation(nameof(LeavesTheFilesOfAnAlreadyDeliveredJobAlone));
        SetupStagedAssets(jobId);
        var first = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), supplier, CancellationToken.None);
        assetHandlerMock.Invocations.Clear();

        var second = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), supplier, CancellationToken.None);

        Assert.AreEqual(DeliveryCreationStatus.AlreadyDelivered, second.Status);
        Assert.AreEqual(first.DeliveryId, second.DeliveryId);
        assetHandlerMock.Verify(a => a.RecordJobAssetsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        assetHandlerMock.Verify(a => a.PromoteJobAssets(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task CreatesTheDeliveryOfARunFromAnotherInstance()
    {
        // The job store of this instance is empty and the run's heartbeat is long stale: the settled protocol is the answer.
        var (jobId, supplier) = PrepareCreation(nameof(CreatesTheDeliveryOfARunFromAnotherInstance));
        SetupStagedAssets(jobId);

        var result = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), supplier, CancellationToken.None);

        Assert.AreEqual(DeliveryCreationStatus.Created, result.Status);
    }

    [TestMethod]
    [DataRow(1, DeliveryCreationStatus.Created, DisplayName = "Attempt alive by its heartbeat, as for its own instance")]
    [DataRow(4, DeliveryCreationStatus.JobNotFound, DisplayName = "Attempt whose instance died before its delivery")]
    public async Task CreatesTheDeliveryOfAnAttemptOnlyWhileItIsAlive(int heartbeatMinutesAgo, DeliveryCreationStatus expected)
    {
        var mandate = AddMandate(nameof(CreatesTheDeliveryOfAnAttemptOnlyWhileItIsAlive));
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" }).Entity;
        context.SaveChanges();
        var supplier = Supplier.ForClient(client.Id);

        // Ended deliverable but undelivered: the attempt is not settled yet, so its heartbeat decides whether it answers.
        var jobId = context.AddRun(mandate.Id, submissionMandateKey: "KEY", heartbeatAgo: TimeSpan.FromMinutes(heartbeatMinutesAgo));
        mandateServiceMock.Setup(m => m.MayDeliverAsync(mandate.Id, supplier)).ReturnsAsync(true);
        SetupStagedAssets(jobId);

        var result = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), supplier, CancellationToken.None);

        Assert.AreEqual(expected, result.Status);
    }

    [TestMethod]
    public async Task RefusesARunThatIsNotDeliverable()
    {
        var mandate = AddMandate(nameof(RefusesARunThatIsNotDeliverable));
        context.SaveChanges();
        var jobId = SetupDeliverableJob(mandate.Id, terminalState: ProcessingState.Failed);

        var result = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), Supplier.ForUser(1), CancellationToken.None);

        Assert.AreEqual(DeliveryCreationStatus.JobNotDeliverable, result.Status);
    }

    [TestMethod]
    public async Task RefusesARunPastItsRetention()
    {
        var mandate = AddMandate(nameof(RefusesARunPastItsRetention));
        context.SaveChanges();
        var jobId = SetupDeliverableJob(mandate.Id, terminalAgo: JobRetention + TimeSpan.FromMinutes(1));

        var result = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), Supplier.ForUser(1), CancellationToken.None);

        Assert.AreEqual(DeliveryCreationStatus.JobNotFound, result.Status);
    }

    [TestMethod]
    public async Task RefusesARunWithoutPermission()
    {
        var mandate = AddMandate(nameof(RefusesARunWithoutPermission));
        context.SaveChanges();
        var jobId = SetupDeliverableJob(mandate.Id, PermissionToDeliver.NotPermitted);

        var result = await service.CreateAsync(jobId, new DeliveryFields(null, null, null), Supplier.ForUser(1), CancellationToken.None);

        // Refused before the supplier or the mandate is looked at: the strict mocks would fail on any further call.
        Assert.AreEqual(DeliveryCreationStatus.JobNotPermitted, result.Status);
    }

    [TestMethod]
    [DataRow(true, true, DeliveryCreationStatus.Created, DisplayName = "A member, on a mandate that takes deliveries")]
    [DataRow(false, true, DeliveryCreationStatus.MandateNotDeliverable, DisplayName = "Someone outside its organisations, on a public mandate")]
    [DataRow(true, false, DeliveryCreationStatus.MandateNotDeliverable, DisplayName = "A member, on a mandate that stopped taking deliveries")]
    public async Task CreatesOnlyForACallerWhoMayDeliverNow(bool callerIsMember, bool allowDelivery, DeliveryCreationStatus expected)
    {
        var (member, mandate) = context.AddMandateWithUserOrganisation(new Mandate
        {
            Name = TestHelpers.Localized(nameof(CreatesOnlyForACallerWhoMayDeliverNow)),
            IsPublic = true,
            AllowDelivery = allowDelivery,
            EvaluateComment = FieldEvaluationType.NotEvaluated,
            EvaluatePartial = FieldEvaluationType.NotEvaluated,
            EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
        });
        var caller = callerIsMember ? member : context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        context.SaveChanges();

        // Permitted at its start, so only who asks now and what the mandate allows now decide.
        var jobId = SetupDeliverableJob(mandate.Id);
        SetupStagedAssets(jobId);

        // The real mandate rules rather than a mock of one lookup, so this holds whichever lookup the creation uses.
        var creationService = new DeliveryCreationService(
            Mock.Of<ILogger<DeliveryCreationService>>(),
            context,
            new MandateService(context, Mock.Of<IUploadStore>(), Mock.Of<IPipelineService>()),
            assetHandlerMock.Object,
            pipelineFactoryMock.Object,
            Options.Create(new DeliveryOptions()),
            Options.Create(new ProcessingOptions { JobTimeout = TimeSpan.FromMinutes(5), JobRetention = JobRetention }));

        var result = await creationService.CreateAsync(jobId, new DeliveryFields(null, null, null), Supplier.ForUser(caller.Id), CancellationToken.None);

        Assert.AreEqual(expected, result.Status);
    }

    [TestMethod]
    public async Task CreatesTheTransfersOfTheChosenSetOnHold()
    {
        var mandate = AddMandate(nameof(CreatesTheTransfersOfTheChosenSetOnHold));
        mandate.ReactionSetId = "ablage_pro_mandat";
        mandate.ReactionSetValues = new() { ["weitergabe"] = new() { ["subfolder"] = JsonSerializer.SerializeToElement("kanton-a") } };
        context.SaveChanges();
        SetupSet("ablage_pro_mandat", "weitergabe", new Parameterization { ["mode"] = "copy" }, "subfolder");

        var deliveryId = await CreateAsync(mandate);

        var delivery = context.Deliveries.AsNoTracking().Single(d => d.Id == deliveryId);
        var reaction = Assert.ContainsSingle(context.DeliveryReactions.AsNoTracking().Where(r => r.DeliveryId == deliveryId).ToList());
        Assert.AreEqual("weitergabe", reaction.ReactionId);
        Assert.AreEqual(ReactionKind.Transfer, reaction.Kind);
        Assert.AreEqual(ReactionState.Pending, reaction.State);
        Assert.AreEqual(FileDrop, reaction.Implementation);
        Assert.AreEqual(delivery.Date.AddHours(1), reaction.NextAttemptAt, "The transfer waits until the uploader can no longer delete the delivery.");

        Assert.AreEqual("copy", reaction.Config.Config["mode"]?.ToString());
        Assert.AreEqual("kanton-a", reaction.Config.Mandate["subfolder"].GetString());
    }

    [TestMethod]
    public async Task KeepsOnlyTheMandateValuesTheSetStillDeclares()
    {
        // The mandate was saved while the set still declared "port"; the definition dropped it since.
        var mandate = AddMandate(nameof(KeepsOnlyTheMandateValuesTheSetStillDeclares));
        mandate.ReactionSetId = "ablage_pro_mandat";
        mandate.ReactionSetValues = new()
        {
            ["weitergabe"] = new() { ["subfolder"] = JsonSerializer.SerializeToElement("kanton-a"), ["port"] = JsonSerializer.SerializeToElement("21") },
        };
        context.SaveChanges();
        SetupSet("ablage_pro_mandat", "weitergabe", new Parameterization(), "subfolder");

        var deliveryId = await CreateAsync(mandate);

        var reaction = Assert.ContainsSingle(context.DeliveryReactions.AsNoTracking().Where(r => r.DeliveryId == deliveryId).ToList());
        CollectionAssert.AreEqual(new[] { "subfolder" }, reaction.Config.Mandate.Keys.ToArray());
    }

    [TestMethod]
    public async Task CreatesTheNotificationsOfTheChosenSetOnHold()
    {
        var mandate = AddMandate(nameof(CreatesTheNotificationsOfTheChosenSetOnHold));
        mandate.ReactionSetId = "ablage_mit_meldung";
        mandate.ReactionSetValues = new() { ["meldung"] = new() { ["subfolder"] = JsonSerializer.SerializeToElement("meldungen") } };
        context.SaveChanges();
        SetupSet("ablage_mit_meldung", [(Reaction("weitergabe"), new Parameterization())], [(Reaction("meldung", "subfolder"), new Parameterization())]);

        var deliveryId = await CreateAsync(mandate);

        var delivery = context.Deliveries.AsNoTracking().Single(d => d.Id == deliveryId);
        var reactions = context.DeliveryReactions.AsNoTracking().Where(r => r.DeliveryId == deliveryId).ToList();
        Assert.HasCount(2, reactions);
        Assert.AreEqual(ReactionKind.Transfer, reactions.Single(r => r.ReactionId == "weitergabe").Kind);
        var notification = reactions.Single(r => r.ReactionId == "meldung");
        Assert.AreEqual(ReactionKind.Notification, notification.Kind);
        Assert.AreEqual(ReactionState.Pending, notification.State);
        Assert.AreEqual(delivery.Date.AddHours(1), notification.NextAttemptAt, "A notification waits like a transfer until the uploader can no longer delete the delivery.");
        Assert.AreEqual("meldungen", notification.Config.Mandate["subfolder"].GetString());
    }

    [TestMethod]
    public async Task CreatesNoReactionsWithoutASet()
    {
        var mandate = AddMandate(nameof(CreatesNoReactionsWithoutASet));
        context.SaveChanges();

        var deliveryId = await CreateAsync(mandate);

        Assert.IsFalse(context.DeliveryReactions.Any(r => r.DeliveryId == deliveryId));
    }

    private void SetupSet(string setId, string reactionId, Parameterization config, params string[] mandateKeys)
        => SetupSet(setId, [(Reaction(reactionId, mandateKeys), config)], []);

    private void SetupSet(string setId, (ReactionConfig Reaction, Parameterization Config)[] transfers, (ReactionConfig Reaction, Parameterization Config)[] notifications)
    {
        var set = new ReactionSetConfig
        {
            Id = setId,
            DisplayName = TestHelpers.Localized(setId),
            Transfers = transfers.Select(transfer => transfer.Reaction).ToList(),
            Notifications = notifications.Select(notification => notification.Reaction).ToList(),
        };
        pipelineFactoryMock.SetupGet(factory => factory.ReactionSets).Returns([set]);
        pipelineFactoryMock.Setup(factory => factory.ResolveTransfers(setId)).Returns(transfers.Select(transfer => (transfer.Reaction.Id, FileDrop, transfer.Config)).ToList());
        pipelineFactoryMock.Setup(factory => factory.ResolveNotifications(setId)).Returns(notifications.Select(notification => (notification.Reaction.Id, FileDrop, notification.Config)).ToList());
    }

    private static ReactionConfig Reaction(string reactionId, params string[] mandateKeys)
        => new()
        {
            Id = reactionId,
            DisplayName = TestHelpers.Localized(reactionId),
            ChannelId = "ablage",
            MandateConfig = mandateKeys.Select(key => new MandateConfigEntry { Key = key, DisplayName = TestHelpers.Localized(key) }).ToList(),
        };

    [TestMethod]
    public async Task KeepsACopyOfTheMetadataFieldsWithTheirValues()
    {
        var mandate = AddMandateWithMetadataFields(nameof(KeepsACopyOfTheMetadataFieldsWithTheirValues));

        var deliveryId = await CreateAsync(mandate, MetadataValues(("responsible", "canton")));

        var delivery = context.Deliveries.AsNoTracking().Single(d => d.Id == deliveryId);
        Assert.HasCount(2, delivery.Metadata);
        Assert.AreEqual("canton", delivery.Metadata[0].Value?.GetString());
        Assert.HasCount(2, delivery.Metadata[0].Options ?? []);
        Assert.IsNull(delivery.Metadata[1].Value);
    }

    [TestMethod]
    public async Task KeepsTheCopyWhenTheMandateChangesLater()
    {
        var mandate = AddMandateWithMetadataFields(nameof(KeepsTheCopyWhenTheMandateChangesLater));
        var deliveryId = await CreateAsync(mandate, MetadataValues(("responsible", "canton")));

        mandate.MetadataFields =
        [
            mandate.MetadataFields[0] with { Title = TestHelpers.Localized("Renamed"), Options = [new MetadataFieldOption("canton", TestHelpers.Localized("Canton"))] },
            mandate.MetadataFields[1],
        ];
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var delivery = context.Deliveries.AsNoTracking().Single(d => d.Id == deliveryId);
        Assert.AreEqual("Responsible", delivery.Metadata[0].Title.GetDisplayText());
        CollectionAssert.AreEqual(new[] { "canton", "municipality" }, (delivery.Metadata[0].Options ?? []).Select(o => o.Id).ToArray());
    }

    [TestMethod]
    public async Task RefusesTheDeliveryWhenTheMandateGainedARequiredField()
    {
        var mandate = AddMandateWithMetadataFields(nameof(RefusesTheDeliveryWhenTheMandateGainedARequiredField));
        var fields = MetadataValues(("responsible", "canton"));
        mandate.MetadataFields = [mandate.MetadataFields[0], mandate.MetadataFields[1] with { Evaluation = FieldEvaluationType.Required }];
        context.SaveChanges();
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" }).Entity;
        context.SaveChanges();
        var supplier = Supplier.ForClient(client.Id);
        var jobId = SetupDeliverableJob(mandate.Id);
        mandateServiceMock.Setup(m => m.MayDeliverAsync(mandate.Id, supplier)).ReturnsAsync(true);

        var result = await service.CreateAsync(jobId, fields, supplier, CancellationToken.None);

        Assert.AreEqual(DeliveryCreationStatus.FieldRulesViolated, result.Status);
        Assert.IsNotNull(result.FieldErrors);
        Assert.IsTrue(result.FieldErrors.ContainsKey("metadata.project_number"));
        Assert.IsFalse(context.Deliveries.Any(d => d.JobId == jobId));
    }

    private Mandate AddMandateWithMetadataFields(string name)
    {
        var mandate = AddMandate(name);
        mandate.MetadataFields =
        [
            new MetadataField(
                "responsible",
                TestHelpers.Localized("Responsible"),
                MetadataFieldType.Selection,
                FieldEvaluationType.Required,
                [new MetadataFieldOption("canton", TestHelpers.Localized("Canton")), new MetadataFieldOption("municipality", TestHelpers.Localized("Municipality"))]),
            new MetadataField("project_number", TestHelpers.Localized("Project number"), MetadataFieldType.Text, FieldEvaluationType.Optional),
        ];
        context.SaveChanges();
        return mandate;
    }

    private static DeliveryFields MetadataValues(params (string Id, string Value)[] values)
        => new(null, null, null, values.ToDictionary(v => v.Id, v => JsonSerializer.SerializeToElement(v.Value)));

    private Mandate AddMandate(string name)
        => context.Mandates.Add(new Mandate
        {
            Name = TestHelpers.Localized(name),
            IsPublic = true,
            AllowDelivery = true,
            EvaluateComment = FieldEvaluationType.NotEvaluated,
            EvaluatePartial = FieldEvaluationType.NotEvaluated,
            EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
        }).Entity;

    private async Task<int> CreateAsync(Mandate mandate, DeliveryFields? fields = null)
    {
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" }).Entity;
        context.SaveChanges();

        var supplier = Supplier.ForClient(client.Id);
        var jobId = SetupDeliverableJob(mandate.Id);
        mandateServiceMock.Setup(m => m.MayDeliverAsync(mandate.Id, supplier)).ReturnsAsync(true);
        SetupStagedAssets(jobId);

        var result = await service.CreateAsync(jobId, fields ?? new DeliveryFields(null, null, null), supplier, CancellationToken.None);

        Assert.AreEqual(DeliveryCreationStatus.Created, result.Status);
        return result.DeliveryId ?? throw new AssertFailedException("A created delivery has an id.");
    }

    private (Guid JobId, Supplier Supplier) PrepareCreation(string mandateName)
    {
        var mandate = AddMandate(mandateName);
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" }).Entity;
        context.SaveChanges();

        var supplier = Supplier.ForClient(client.Id);
        var jobId = SetupDeliverableJob(mandate.Id);
        mandateServiceMock.Setup(m => m.MayDeliverAsync(mandate.Id, supplier)).ReturnsAsync(true);
        return (jobId, supplier);
    }

    private int SaveDeliveryOfAnotherCaller(Guid jobId)
    {
        var delivery = context.Deliveries.Add(new Delivery
        {
            JobId = jobId,
            Mandate = context.Mandates.First(),
            DeliveredByUser = context.Users.First(),
            Comment = string.Empty,
            Assets = new List<Asset>(),
        }).Entity;
        context.SaveChanges();
        return delivery.Id;
    }

    private void SetupStagedAssets(Guid jobId)
    {
        assetHandlerMock.Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Asset> { new Asset() });
        assetHandlerMock.Setup(a => a.PromoteJobAssets(jobId));
    }

    private Guid SetupDeliverableJob(
        int mandateId,
        PermissionToDeliver permission = PermissionToDeliver.Permitted,
        ProcessingState terminalState = ProcessingState.Success,
        TimeSpan? terminalAgo = null)
        => context.AddRun(mandateId, terminalState, permission, terminalAgo);
}
