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
/// service up for real but only ever declares as a user. What is checked here is the other declarer.
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
        mandateServiceMock.Setup(m => m.GetMandateForDeliveryAsync(mandate.Id, declarer)).ReturnsAsync(mandate);
        assetHandlerMock.Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Asset> { new Asset() });

        var result = await service.DeclareAsync(jobId, new DeliveryFields(null, null, null), declarer, CancellationToken.None);

        Assert.AreEqual(DeliveryDeclarationStatus.Created, result.Status);
        var delivery = await context.Deliveries.SingleAsync(d => d.Id == result.DeliveryId);
        Assert.AreEqual(client.Id, delivery.DeclaringClientId);
        Assert.IsNull(delivery.DeclaringUserId, "A delivery has exactly one declarer; the database constraint says the same.");
        Assert.AreEqual("SILENTHARBOR", delivery.DeclarerName, "The client's name is what the catalog shows as the deliverer.");
    }

    [TestMethod]
    public async Task CreatesTheTransfersOfTheChosenSetOnHold()
    {
        var mandate = AddMandate(nameof(CreatesTheTransfersOfTheChosenSetOnHold));
        mandate.ReactionSetId = "ablage";
        mandate.ReactionSetValues = new() { ["ablage"] = new() { ["subfolder"] = JsonSerializer.SerializeToElement("kanton-a") } };
        context.SaveChanges();
        pipelineFactoryMock
            .Setup(factory => factory.ResolveTransfers("ablage"))
            .Returns([("ablage", FileDrop, new Parameterization { ["mode"] = "copy" })]);

        var deliveryId = await DeclareAsync(mandate);

        var delivery = context.Deliveries.AsNoTracking().Single(d => d.Id == deliveryId);
        var reaction = Assert.ContainsSingle(context.DeliveryReactions.AsNoTracking().Where(r => r.DeliveryId == deliveryId).ToList());
        Assert.AreEqual("ablage", reaction.ReactionId);
        Assert.AreEqual(ReactionKind.Transfer, reaction.Kind);
        Assert.AreEqual(ReactionState.Pending, reaction.State);
        Assert.AreEqual(FileDrop, reaction.Implementation);
        Assert.AreEqual(delivery.Date.AddHours(1), reaction.NextAttemptAt, "The transfer waits until the uploader can no longer delete the delivery.");

        using var snapshot = JsonDocument.Parse(reaction.Config);
        Assert.AreEqual("copy", snapshot.RootElement.GetProperty("config").GetProperty("mode").GetString());
        Assert.AreEqual("kanton-a", snapshot.RootElement.GetProperty("mandate").GetProperty("subfolder").GetString());
    }

    [TestMethod]
    public async Task CreatesNoReactionsWithoutASet()
    {
        var mandate = AddMandate(nameof(CreatesNoReactionsWithoutASet));
        context.SaveChanges();

        var deliveryId = await DeclareAsync(mandate);

        Assert.IsFalse(context.DeliveryReactions.Any(r => r.DeliveryId == deliveryId));
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
        mandateServiceMock.Setup(m => m.GetMandateForDeliveryAsync(mandate.Id, declarer)).ReturnsAsync(mandate);
        assetHandlerMock.Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Asset> { new Asset() });

        var result = await service.DeclareAsync(jobId, new DeliveryFields(null, null, null), declarer, CancellationToken.None);

        Assert.AreEqual(DeliveryDeclarationStatus.Created, result.Status);
        return result.DeliveryId ?? throw new AssertFailedException("A created delivery has an id.");
    }

    private Guid SetupDeliverableJob(int mandateId)
    {
        var jobId = Guid.NewGuid();
        processingServiceMock
            .Setup(s => s.GetJob(jobId))
            .Returns(new ProcessingJob(jobId, Guid.NewGuid(), mandateId, DateTime.Now) { State = ProcessingState.Success });
        return jobId;
    }
}
