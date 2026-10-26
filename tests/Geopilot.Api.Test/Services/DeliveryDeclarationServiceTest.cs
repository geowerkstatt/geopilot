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
/// The declaration rules are covered through <see cref="Controllers.DeliveryControllerTest"/>, which wires the
/// service up for real but only ever declares as a user and mocks the mandate rules. What is checked here is the
/// other declarer, and who may declare against the real mandate rules.
/// </summary>
[TestClass]
public class DeliveryDeclarationServiceTest
{
    private const string FileDrop = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel";

    private Context context;
    private Mock<IProcessingService> processingServiceMock;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IAssetHandler> assetHandlerMock;
    private Mock<IPipelineFactory> pipelineFactoryMock;
    private DeliveryDeclarationService service;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        processingServiceMock = new Mock<IProcessingService>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        assetHandlerMock = new Mock<IAssetHandler>(MockBehavior.Strict);
        pipelineFactoryMock = new Mock<IPipelineFactory>(MockBehavior.Strict);
        service = new DeliveryDeclarationService(
            Mock.Of<ILogger<DeliveryDeclarationService>>(),
            context,
            processingServiceMock.Object,
            mandateServiceMock.Object,
            assetHandlerMock.Object,
            pipelineFactoryMock.Object,
            Options.Create(new DeliveryOptions { UploaderDeleteEnabled = true, DeleteDuration = TimeSpan.FromHours(1) }));
    }

    [TestCleanup]
    public void Cleanup() => context.Dispose();

    [TestMethod]
    public async Task DeclaresTheDeliveryForAMachineClient()
    {
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" }).Entity;
        var mandate = context.Mandates.Add(new Mandate
        {
            Name = TestHelpers.Localized(nameof(DeclaresTheDeliveryForAMachineClient)),
            IsPublic = true,
            AllowDelivery = true,
            EvaluateComment = FieldEvaluationType.NotEvaluated,
            EvaluatePartial = FieldEvaluationType.NotEvaluated,
            EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
        }).Entity;
        context.SaveChanges();

        var declarer = Declarer.ForClient(client.Id);
        var jobId = SetupDeliverableJob(mandate.Id);
        mandateServiceMock.Setup(m => m.IsDeclarableAsync(mandate.Id, declarer)).ReturnsAsync(true);
        assetHandlerMock.Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Asset> { new Asset() });

        var result = await service.DeclareAsync(jobId, new DeliveryFields(null, null, null), declarer, CancellationToken.None);

        Assert.AreEqual(DeliveryDeclarationStatus.Created, result.Status);
        var delivery = await context.Deliveries.SingleAsync(d => d.Id == result.DeliveryId);
        Assert.AreEqual(client.Id, delivery.DeclaringClientId);
        Assert.IsNull(delivery.DeclaringUserId, "A delivery has exactly one declarer; the database constraint says the same.");
        Assert.AreEqual("SILENTHARBOR", delivery.DeclarerName, "The client's name is what the catalog shows as the deliverer.");
    }

    [TestMethod]
    public async Task RefusesARunNobodyCanDeclare()
    {
        var jobId = SetupDeliverableJob(mandateId: 1, isDeclarable: false);

        var result = await service.DeclareAsync(jobId, new DeliveryFields(null, null, null), Declarer.ForUser(1), CancellationToken.None);

        // Refused before the declarer or the mandate is looked at: the strict mocks would fail on any further call.
        Assert.AreEqual(DeliveryDeclarationStatus.JobNotDeclarable, result.Status);
    }

    [TestMethod]
    [DataRow(true, true, DeliveryDeclarationStatus.Created, DisplayName = "A member, on a mandate that takes deliveries")]
    [DataRow(false, true, DeliveryDeclarationStatus.MandateNotDeliverable, DisplayName = "Someone outside its organisations, on a public mandate")]
    [DataRow(true, false, DeliveryDeclarationStatus.MandateNotDeliverable, DisplayName = "A member, on a mandate that stopped taking deliveries")]
    public async Task DeclaresOnlyForACallerWhoMayDeliverNow(bool callerIsMember, bool allowDelivery, DeliveryDeclarationStatus expected)
    {
        var (member, mandate) = context.AddMandateWithUserOrganisation(new Mandate
        {
            Name = TestHelpers.Localized(nameof(DeclaresOnlyForACallerWhoMayDeliverNow)),
            IsPublic = true,
            AllowDelivery = allowDelivery,
            EvaluateComment = FieldEvaluationType.NotEvaluated,
            EvaluatePartial = FieldEvaluationType.NotEvaluated,
            EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
        });
        var caller = callerIsMember ? member : context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        context.SaveChanges();

        // Declarable at its start, so only who asks now and what the mandate allows now decide.
        var jobId = SetupDeliverableJob(mandate.Id);
        assetHandlerMock.Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Asset> { new Asset() });

        // The real mandate rules rather than a mock of one lookup, so this holds whichever lookup the declaration uses.
        var declaringService = new DeliveryDeclarationService(
            Mock.Of<ILogger<DeliveryDeclarationService>>(),
            context,
            processingServiceMock.Object,
            new MandateService(context, Mock.Of<IUploadStore>(), Mock.Of<IPipelineService>()),
            assetHandlerMock.Object,
            pipelineFactoryMock.Object,
            Options.Create(new DeliveryOptions()));

        var result = await declaringService.DeclareAsync(jobId, new DeliveryFields(null, null, null), Declarer.ForUser(caller.Id), CancellationToken.None);

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

        var deliveryId = await DeclareAsync(mandate);

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

        var deliveryId = await DeclareAsync(mandate);

        var reaction = Assert.ContainsSingle(context.DeliveryReactions.AsNoTracking().Where(r => r.DeliveryId == deliveryId).ToList());
        CollectionAssert.AreEqual(new[] { "subfolder" }, reaction.Config.Mandate.Keys.ToArray());
    }

    [TestMethod]
    public async Task CreatesNoReactionsWithoutASet()
    {
        var mandate = AddMandate(nameof(CreatesNoReactionsWithoutASet));
        context.SaveChanges();

        var deliveryId = await DeclareAsync(mandate);

        Assert.IsFalse(context.DeliveryReactions.Any(r => r.DeliveryId == deliveryId));
    }

    private void SetupSet(string setId, string reactionId, Parameterization config, params string[] mandateKeys)
    {
        var reaction = new ReactionConfig
        {
            Id = reactionId,
            ChannelId = "ablage",
            MandateConfig = mandateKeys.Select(key => new MandateConfigEntry { Key = key, DisplayName = TestHelpers.Localized(key) }).ToList(),
        };
        var set = new ReactionSetConfig { Id = setId, DisplayName = TestHelpers.Localized(setId), Transfers = [reaction] };
        pipelineFactoryMock.SetupGet(factory => factory.ReactionSets).Returns([set]);
        pipelineFactoryMock.Setup(factory => factory.ResolveTransfers(setId)).Returns([(reactionId, FileDrop, config)]);
    }

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

    private async Task<int> DeclareAsync(Mandate mandate)
    {
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" }).Entity;
        context.SaveChanges();

        var declarer = Declarer.ForClient(client.Id);
        var jobId = SetupDeliverableJob(mandate.Id);
        mandateServiceMock.Setup(m => m.IsDeclarableAsync(mandate.Id, declarer)).ReturnsAsync(true);
        assetHandlerMock.Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Asset> { new Asset() });

        var result = await service.DeclareAsync(jobId, new DeliveryFields(null, null, null), declarer, CancellationToken.None);

        Assert.AreEqual(DeliveryDeclarationStatus.Created, result.Status);
        return result.DeliveryId ?? throw new AssertFailedException("A created delivery has an id.");
    }

    private Guid SetupDeliverableJob(int mandateId, bool isDeclarable = true)
    {
        var jobId = Guid.NewGuid();
        processingServiceMock
            .Setup(s => s.GetJob(jobId))
            .Returns(new ProcessingJob(jobId, Guid.NewGuid(), mandateId, DateTime.Now) { State = ProcessingState.Success, IsDeclarable = isDeclarable });
        return jobId;
    }
}
