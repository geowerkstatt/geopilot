using Geopilot.Api.Enums;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Services;

/// <summary>
/// The declaration rules are covered through <see cref="Controllers.DeliveryControllerTest"/>, which wires the
/// service up for real but only ever declares as a user. What is checked here is the other declarer.
/// </summary>
[TestClass]
public class DeliveryDeclarationServiceTest
{
    private Context context;
    private Mock<IProcessingService> processingServiceMock;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IAssetHandler> assetHandlerMock;
    private DeliveryDeclarationService service;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        processingServiceMock = new Mock<IProcessingService>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        assetHandlerMock = new Mock<IAssetHandler>(MockBehavior.Strict);
        service = new DeliveryDeclarationService(
            Mock.Of<ILogger<DeliveryDeclarationService>>(),
            context,
            processingServiceMock.Object,
            mandateServiceMock.Object,
            assetHandlerMock.Object);
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
        mandateServiceMock.Setup(m => m.GetMandateForDeclarerAsync(mandate.Id, declarer)).ReturnsAsync(mandate);
        assetHandlerMock.Setup(a => a.RecordJobAssetsAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Asset> { new Asset() });

        var result = await service.DeclareAsync(jobId, new DeliveryFields(null, null, null), declarer, CancellationToken.None);

        Assert.AreEqual(DeliveryDeclarationStatus.Created, result.Status);
        var delivery = await context.Deliveries.SingleAsync(d => d.Id == result.DeliveryId);
        Assert.AreEqual(client.Id, delivery.DeclaringClientId);
        Assert.IsNull(delivery.DeclaringUserId, "A delivery has exactly one declarer; the database constraint says the same.");
        Assert.AreEqual("SILENTHARBOR", delivery.DeclarerName, "The client's name is what the catalog shows as the deliverer.");
    }

    private Guid SetupDeliverableJob(int mandateId)
    {
        var jobId = Guid.NewGuid();
        var pipelineMock = new Mock<IPipeline>();
        pipelineMock.SetupGet(p => p.State).Returns(ProcessingState.Success);
        pipelineMock.SetupGet(p => p.Steps).Returns(new List<IPipelineStep>());
        pipelineMock.SetupGet(p => p.DisplayName).Returns(LocalizedText.Empty);

        processingServiceMock
            .Setup(s => s.GetJob(jobId))
            .Returns(new ProcessingJob(jobId, Guid.NewGuid(), mandateId, DateTime.Now) { Pipeline = pipelineMock.Object });
        return jobId;
    }
}
