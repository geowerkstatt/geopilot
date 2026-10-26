using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace Geopilot.Api.Reactions;

[TestClass]
public class ReactionWorkerTest
{
    private const string FileDrop = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel";
    private const string FileName = "Daten.xtf";
    private const string FileContent = "<TRANSFER/>";

    private static readonly string Probe = typeof(ProbeChannel).FullName ?? string.Empty;
    private static readonly ChannelResult Delivered = ChannelResult.Success("Delivered to the target.");

    private readonly List<Guid> jobIds = [];

    private Context context;
    private DateTime dbNow;
    private PhysicalAssetFileStore assetStore;
    private DeliveryReactionOptions reactionOptions;
    private PipelineOptions pipelineOptions;
    private Mock<ILogger<ReactionWorker>> loggerMock;
    private Mock<IPipelineRunRecorder> recorderMock;
    private User user;
    private Delivery delivery;

    [TestInitialize]
    public async Task Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();

        // now() stands still within the test transaction, so a reaction is due when it was due at this time.
        dbNow = await context.Database.SqlQuery<DateTime>($"SELECT now() AS \"Value\"").SingleAsync();
        assetStore = new PhysicalAssetFileStore(AssemblyInitialize.TestDirectoryProvider);

        // Two waits between attempts make three attempts.
        reactionOptions = new DeliveryReactionOptions { RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)] };
        pipelineOptions = new PipelineOptions { Definition = "pipeline.yaml" };
        loggerMock = new Mock<ILogger<ReactionWorker>>();
        recorderMock = new Mock<IPipelineRunRecorder>();

        user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString(), FullName = "Erika Muster", Email = "erika.muster@example.com" }).Entity;
        delivery = AddDelivery(declaringUser: user);
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var jobId in jobIds)
        {
            assetStore.DeleteJob(jobId);
        }

        context.Dispose();
    }

    [TestMethod]
    public async Task RunNextReportsThatNoReactionIsDue()
    {
        AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(Delivered))), nextAttemptAt: dbNow.AddMinutes(1));

        using var worker = CreateWorker();
        Assert.IsFalse(await worker.RunNextAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task RunNextFinishesADeliveredReaction()
    {
        var reaction = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(Delivered))));

        using var worker = CreateWorker();
        Assert.IsTrue(await worker.RunNextAsync(CancellationToken.None));

        var stored = await ReloadAsync(reaction);
        Assert.AreEqual(ReactionState.Done, stored.State);
        Assert.AreEqual("Delivered to the target.", stored.Message);
    }

    [TestMethod]
    public async Task RunNextRecordsTheAttemptInTheProtocol()
    {
        var reaction = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(Delivered))));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        recorderMock.Verify(recorder => recorder.RecordReactionAttemptStartedAsync(It.Is<DeliveryReaction>(claimed => claimed.Id == reaction.Id && claimed.Attempts == 1)), Times.Once);
        recorderMock.Verify(recorder => recorder.RecordReactionAttemptFinishedAsync(It.Is<DeliveryReaction>(claimed => claimed.Id == reaction.Id), ReactionState.Done, "Delivered to the target."), Times.Once);
    }

    [TestMethod]
    public async Task RunNextGivesTheChannelTheDeliveryAndItsFiles()
    {
        var precursor = AddDelivery(declaringUser: user);
        var withPrecursor = AddDelivery(declaringUser: user, precursor: precursor);
        var probe = new ChannelProbe(Answer(Delivered));
        AddReaction(Probe, ProbeSnapshot(probe), of: withPrecursor);

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var info = probe.Delivery;
        Assert.IsNotNull(info);
        Assert.AreEqual(withPrecursor.Id, info.Id);
        Assert.AreEqual(withPrecursor.JobId, info.JobId);
        Assert.AreEqual(new DateTimeOffset(DateTime.SpecifyKind(withPrecursor.Date, DateTimeKind.Utc)), info.DeclaredAt);
        Assert.IsTrue(info.Partial);
        Assert.AreEqual(precursor.Id, info.PrecursorDeliveryId);
        Assert.IsNull(info.Comment, "An empty comment is no comment.");
        Assert.AreEqual(withPrecursor.Mandate.Id, info.Mandate.Id);
        Assert.AreEqual(withPrecursor.Mandate.Name, info.Mandate.Name);
        Assert.AreEqual(withPrecursor.Mandate.Key, info.Mandate.Key);
        Assert.AreEqual(DeclarerKind.User, info.Declarer.Kind);
        Assert.AreEqual("Erika Muster", info.Declarer.Name);
        Assert.AreEqual("erika.muster@example.com", info.Declarer.Email);
        CollectionAssert.AreEqual(new[] { (FileName, FileContent) }, probe.Files.ToArray());
    }

    [TestMethod]
    public async Task RunNextNamesAMachineClientWithoutAnEmailAddress()
    {
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" }).Entity;
        var probe = new ChannelProbe(Answer(Delivered));
        AddReaction(Probe, ProbeSnapshot(probe), of: AddDelivery(declaringClient: client));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        Assert.IsNotNull(probe.Delivery);
        Assert.AreEqual(DeclarerKind.MachineClient, probe.Delivery.Declarer.Kind);
        Assert.AreEqual("SILENTHARBOR", probe.Delivery.Declarer.Name);
        Assert.IsNull(probe.Delivery.Declarer.Email);
    }

    [TestMethod]
    public async Task RunNextBindsTheSnapshotAndTheBaseConfiguration()
    {
        pipelineOptions.ChannelConfigs[Probe] = new Parameterization { ["baseValue"] = "aus der Basis" };
        var probe = new ChannelProbe(Answer(Delivered));

        // A number in the definition is a text from YAML; it has to bind to its parameter after the round trip too.
        AddReaction(Probe, ProbeSnapshot(probe, port: "587", subfolder: "kanton-a"));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "aus der Basis" }, probe.BaseValues.ToArray());
        Assert.AreEqual(587, probe.Port);
        Assert.AreEqual("kanton-a", probe.Subfolder);
    }

    [TestMethod]
    public async Task RunNextReadsTheBaseConfigurationAnewForEveryAttempt()
    {
        var probe = new ChannelProbe(Answer(Delivered));
        AddReaction(Probe, ProbeSnapshot(probe), reactionId: "erste", nextAttemptAt: dbNow.AddMinutes(-2));
        AddReaction(Probe, ProbeSnapshot(probe), reactionId: "zweite", nextAttemptAt: dbNow.AddMinutes(-1));
        using var worker = CreateWorker();

        pipelineOptions.ChannelConfigs[Probe] = new Parameterization { ["baseValue"] = "vorher" };
        await worker.RunNextAsync(CancellationToken.None);
        pipelineOptions.ChannelConfigs[Probe] = new Parameterization { ["baseValue"] = "nachher" };
        await worker.RunNextAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "vorher", "nachher" }, probe.BaseValues.ToArray());
    }

    [TestMethod]
    public async Task RunNextSchedulesTheNextAttemptWhenTheChannelFails()
    {
        var reaction = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(ChannelResult.Failure("The target is not reachable.")))));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var stored = await ReloadAsync(reaction);
        Assert.AreEqual(ReactionState.Pending, stored.State);
        Assert.AreEqual(dbNow.AddMinutes(1), stored.NextAttemptAt);
        Assert.AreEqual("The target is not reachable.", stored.Message);
        VerifyLogged(LogLevel.Warning, Times.Once());
    }

    [TestMethod]
    public async Task RunNextRecordsAnExceptionOfTheChannel()
    {
        var reaction = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(_ => throw new InvalidOperationException("The probe broke."))));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var stored = await ReloadAsync(reaction);
        Assert.AreEqual(ReactionState.Pending, stored.State);
        Assert.AreEqual("The attempt failed unexpectedly (InvalidOperationException); the log has the details.", stored.Message, "the text of an exception can name internal paths or hosts.");
    }

    [TestMethod]
    public async Task RunNextRecordsAChannelThatCannotBeResolved()
    {
        var reaction = AddReaction("Geopilot.Pipeline.Channels.Missing", new ReactionSnapshot(new Parameterization(), []));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var stored = await ReloadAsync(reaction);
        Assert.AreEqual(ReactionState.Pending, stored.State);
        Assert.AreEqual("Channel <Geopilot.Pipeline.Channels.Missing> is unknown.", stored.Message);
    }

    [TestMethod]
    public async Task RunNextRecordsAValueTheChannelRefuses()
    {
        pipelineOptions.ChannelConfigs[FileDrop] = new Parameterization { ["baseDirectory"] = Path.GetTempPath() };
        var reaction = AddReaction(FileDrop, new ReactionSnapshot(new Parameterization(), MandateValues("../anderswo")));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var stored = await ReloadAsync(reaction);
        Assert.AreEqual(ReactionState.Pending, stored.State);
        Assert.AreEqual("The subfolder <../anderswo> is not a relative path without '..'. (Parameter 'subfolder')", stored.Message);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "The channel honours its token")]
    [DataRow(false, DisplayName = "The channel ignores its token")]
    public async Task RunNextStopsAnAttemptAtItsTimeLimit(bool honoursToken)
    {
        reactionOptions.TimeLimit = TimeSpan.FromMilliseconds(200);
        var probe = new ChannelProbe(async token =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), honoursToken ? token : CancellationToken.None);
            return Delivered;
        });
        var reaction = AddReaction(Probe, ProbeSnapshot(probe));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var stored = await ReloadAsync(reaction);
        Assert.AreEqual(ReactionState.Pending, stored.State);
        Assert.AreEqual($"The attempt exceeded its time limit of {reactionOptions.TimeLimit}.", stored.Message);
    }

    [TestMethod]
    public async Task RunNextCountsTheTimeBeforeTheChannelAgainstTheTimeLimit()
    {
        // The lock runs from the claim, so the time limit has to as well, or a slow database could outlast the lock.
        reactionOptions.TimeLimit = TimeSpan.FromMilliseconds(200);
        recorderMock
            .Setup(recorder => recorder.RecordReactionAttemptStartedAsync(It.IsAny<DeliveryReaction>()))
            .Returns(() => Task.Delay(TimeSpan.FromMilliseconds(500)));
        var reaction = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(Delivered))));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var stored = await ReloadAsync(reaction);
        Assert.AreEqual(ReactionState.Pending, stored.State);
        Assert.AreEqual($"The attempt exceeded its time limit of {reactionOptions.TimeLimit}.", stored.Message);
    }

    [TestMethod]
    public async Task RunNextRecordsAnAttemptInterruptedByTheStop()
    {
        using var stopping = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new ChannelProbe(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Delivered;
        });
        var reaction = AddReaction(Probe, ProbeSnapshot(probe));

        using var worker = CreateWorker();
        var run = worker.RunNextAsync(stopping.Token);
        await started.Task;
        await stopping.CancelAsync();

        Assert.IsTrue(await run);
        var stored = await ReloadAsync(reaction);
        Assert.AreEqual(ReactionState.Pending, stored.State);
        Assert.AreEqual("The attempt was interrupted because the application stopped.", stored.Message);
    }

    [TestMethod]
    public async Task RunNextGivesUpATransferAfterItsLastAttemptAndLogsAnError()
    {
        var reaction = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(ChannelResult.Failure("The target is not reachable.")))), attempts: 2);

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        Assert.AreEqual(ReactionState.Error, (await ReloadAsync(reaction)).State);
        VerifyLogged(LogLevel.Error, Times.Once());
    }

    [TestMethod]
    public async Task RunDueReactionsRunsEveryDueReaction()
    {
        var first = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(Delivered))), reactionId: "erste");
        var second = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(Delivered))), reactionId: "zweite");

        using var worker = CreateWorker();
        await worker.RunDueReactionsAsync(CancellationToken.None);

        Assert.AreEqual(ReactionState.Done, (await ReloadAsync(first)).State);
        Assert.AreEqual(ReactionState.Done, (await ReloadAsync(second)).State);
    }

    [TestMethod]
    public async Task RunDueReactionsLogsAFailureAndReturns()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(factory => factory.CreateScope()).Throws(new InvalidOperationException("The database is not reachable."));

        using var worker = CreateWorker(scopeFactoryMock.Object);
        await worker.RunDueReactionsAsync(CancellationToken.None);

        VerifyLogged(LogLevel.Error, Times.Once());
    }

    private static Func<CancellationToken, Task<ChannelResult>> Answer(ChannelResult result) => _ => Task.FromResult(result);

    private static Dictionary<string, JsonElement> MandateValues(string subfolder)
        => new() { ["subfolder"] = JsonSerializer.SerializeToElement(subfolder) };

    private static ReactionSnapshot ProbeSnapshot(ChannelProbe probe, string? port = null, string? subfolder = null)
    {
        var config = new Parameterization { ["probe"] = ProbeChannel.Register(probe) };
        if (port is not null)
            config["port"] = port;

        return new ReactionSnapshot(config, subfolder is null ? [] : MandateValues(subfolder));
    }

    private ReactionWorker CreateWorker(IServiceScopeFactory? scopeFactory = null)
    {
        var options = Options.Create(reactionOptions);
        var pipelineOptionsMock = new Mock<IOptionsMonitor<PipelineOptions>>();
        pipelineOptionsMock.SetupGet(monitor => monitor.CurrentValue).Returns(() => pipelineOptions);

        return new ReactionWorker(
            scopeFactory ?? ScopeFactory(),
            new ReactionStore(options, Mock.Of<ILogger<ReactionStore>>()),
            options,
            pipelineOptionsMock.Object,
            loggerMock.Object);
    }

    // Every attempt gets the context of the test, whose transaction is rolled back afterwards.
    private IServiceScopeFactory ScopeFactory()
    {
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(provider => provider.GetService(typeof(Context))).Returns(context);
        serviceProviderMock.Setup(provider => provider.GetService(typeof(IAssetFileStore))).Returns(assetStore);
        serviceProviderMock.Setup(provider => provider.GetService(typeof(IPipelineRunRecorder))).Returns(recorderMock.Object);
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(scope => scope.ServiceProvider).Returns(serviceProviderMock.Object);
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(factory => factory.CreateScope()).Returns(scopeMock.Object);
        return scopeFactoryMock.Object;
    }

    private void VerifyLogged(LogLevel level, Times times)
        => loggerMock.Verify(
            logger => logger.Log(level, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    private Task<DeliveryReaction> ReloadAsync(DeliveryReaction reaction)
        => context.DeliveryReactions.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == reaction.Id);

    private Delivery AddDelivery(User? declaringUser = null, MachineClient? declaringClient = null, Delivery? precursor = null)
    {
        var newDelivery = new Delivery
        {
            JobId = Guid.NewGuid(),
            Date = dbNow.AddHours(-1),
            Mandate = new Mandate { Name = TestHelpers.Localized("Ablage Kanton A"), Key = $"ablage-{Guid.NewGuid():N}" },
            DeclaringUser = declaringUser,
            DeclaringClient = declaringClient,
            PrecursorDelivery = precursor,
            Partial = true,
            Assets = [new Asset { OriginalFilename = FileName, SanitizedFilename = FileName, FileHash = [1, 2, 3] }],
        };
        context.Deliveries.Add(newDelivery);
        context.SaveChanges();

        jobIds.Add(newDelivery.JobId);
        using var file = new StreamWriter(assetStore.CreateFile(newDelivery.JobId, FileName));
        file.Write(FileContent);
        return newDelivery;
    }

    private DeliveryReaction AddReaction(string implementation, ReactionSnapshot snapshot, int attempts = 0, string reactionId = "weitergabe", DateTime? nextAttemptAt = null, Delivery? of = null)
    {
        var reaction = new DeliveryReaction
        {
            Delivery = of ?? delivery,
            ReactionId = reactionId,
            Kind = ReactionKind.Transfer,
            Implementation = implementation,
            Config = snapshot,
            State = ReactionState.Pending,
            Attempts = attempts,
            NextAttemptAt = nextAttemptAt ?? dbNow.AddSeconds(-1),
        };
        context.DeliveryReactions.Add(reaction);
        context.SaveChanges();
        return reaction;
    }
}
