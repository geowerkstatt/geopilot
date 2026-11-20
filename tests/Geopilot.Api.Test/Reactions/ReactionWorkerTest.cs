using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using System.Text.Json;

namespace Geopilot.Api.Reactions;

[TestClass]
public class ReactionWorkerTest
{
    private const string FileDrop = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel";
    private const string FileName = "Daten.xtf";
    private const string FileContent = "<TRANSFER/>";

    private static readonly string Probe = typeof(ProbeChannel).FullName ?? string.Empty;
    private static readonly ChannelResult Delivered = ChannelResult.Success(TestHelpers.Localized("Delivered to the target."));

    private readonly List<Guid> jobIds = [];

    private Context context;
    private DateTime dbNow;
    private PhysicalAssetFileStore assetStore;
    private DeliveryReactionOptions reactionOptions;
    private PipelineOptions pipelineOptions;
    private Mock<ILogger<ReactionWorker>> loggerMock;
    private Mock<IRecorder> recorderMock;
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
        recorderMock = new Mock<IRecorder>();

        user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString(), FullName = "Erika Muster", Email = "erika.muster@example.com" }).Entity;
        delivery = AddDelivery(deliveredByUser: user);
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
        Assert.AreEqual(TestHelpers.Localized("Delivered to the target."), stored.Message);
    }

    [TestMethod]
    public async Task RunNextRecordsTheAttemptInTheProtocol()
    {
        var reaction = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(Delivered))));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        recorderMock.Verify(recorder => recorder.RecordReactionAttemptStartedAsync(It.Is<DeliveryReaction>(claimed => claimed.Id == reaction.Id && claimed.Attempts == 1), It.IsAny<IReadOnlyDictionary<string, RecordedConfigValue>?>()), Times.Once);
        recorderMock.Verify(recorder => recorder.RecordReactionAttemptFinishedAsync(It.Is<DeliveryReaction>(claimed => claimed.Id == reaction.Id), It.IsAny<IReadOnlyDictionary<string, RecordedConfigValue>?>(), ReactionState.Done, Delivered.Message), Times.Once);
    }

    [TestMethod]
    public async Task RunNextGivesTheChannelTheDeliveryAndItsFiles()
    {
        var precursor = AddDelivery(deliveredByUser: user);
        var withPrecursor = AddDelivery(deliveredByUser: user, precursor: precursor);
        var probe = new ChannelProbe(Answer(Delivered));
        AddReaction(Probe, ProbeSnapshot(probe), of: withPrecursor);

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var info = probe.Delivery;
        Assert.IsNotNull(info);
        Assert.AreEqual(withPrecursor.Id, info.Id);
        Assert.AreEqual(withPrecursor.JobId, info.JobId);
        Assert.AreEqual("weitergabe", info.ReactionId);
        Assert.AreEqual(new DateTimeOffset(DateTime.SpecifyKind(withPrecursor.Date, DateTimeKind.Utc)), info.DeliveredAt);
        Assert.IsTrue(info.Partial);
        Assert.AreEqual(precursor.Id, info.PrecursorDeliveryId);
        Assert.IsNull(info.Comment, "An empty comment is no comment.");
        Assert.AreEqual(withPrecursor.Mandate.Id, info.Mandate.Id);
        Assert.AreEqual(withPrecursor.Mandate.Name, info.Mandate.Name);
        Assert.AreEqual(withPrecursor.Mandate.Key, info.Mandate.Key);
        Assert.AreEqual(SupplierKind.User, info.Supplier.Kind);
        Assert.AreEqual("Erika Muster", info.Supplier.Name);
        Assert.AreEqual("erika.muster@example.com", info.Supplier.Email);
        CollectionAssert.AreEqual(new[] { (FileName, FileContent) }, probe.Files.ToArray());
    }

    [TestMethod]
    public async Task RunNextGivesTheChannelTheMetadataOfTheDelivery()
    {
        var options = new MetadataFieldOption[]
        {
            new("canton", TestHelpers.Localized("Kanton")),
            new("municipality", TestHelpers.Localized("Gemeinde")),
        };
        var metadata = new List<DeliveredMetadataField>
        {
            new("responsible", TestHelpers.Localized("Verantwortlich"), MetadataFieldType.Selection, FieldEvaluationType.Required, options, JsonSerializer.SerializeToElement("canton")),
            new("project_number", TestHelpers.Localized("Projektnummer"), MetadataFieldType.Text, FieldEvaluationType.Optional, null, null),
            new("due_date", TestHelpers.Localized("Termin"), MetadataFieldType.Date, FieldEvaluationType.Optional, null, JsonSerializer.SerializeToElement("2026-11-04")),
        };
        var probe = new ChannelProbe(Answer(Delivered));
        AddReaction(Probe, ProbeSnapshot(probe), of: AddDelivery(deliveredByUser: user, metadata: metadata));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var info = probe.Delivery;
        Assert.IsNotNull(info);
        Assert.HasCount(2, info.Metadata, "A field that was not filled in is left out.");
        Assert.AreEqual("Kanton", info.Metadata[0].Option?.GetDisplayText("de"));
        Assert.AreEqual(new DateOnly(2026, 11, 4), info.Metadata[1].Date);
    }

    [TestMethod]
    public async Task RunNextNamesAMachineClientWithoutAnEmailAddress()
    {
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" }).Entity;
        var probe = new ChannelProbe(Answer(Delivered));
        AddReaction(Probe, ProbeSnapshot(probe), of: AddDelivery(deliveredByClient: client));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        Assert.IsNotNull(probe.Delivery);
        Assert.AreEqual(SupplierKind.MachineClient, probe.Delivery.Supplier.Kind);
        Assert.AreEqual("SILENTHARBOR", probe.Delivery.Supplier.Name);
        Assert.IsNull(probe.Delivery.Supplier.Email);
    }

    [TestMethod]
    public async Task RunNextNamesTheActiveUsersResponsibleForAMachineClient()
    {
        var responsible = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString(), FullName = "Betrieb Fachschale", Email = "betrieb@example.com" }).Entity;
        var deactivated = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString(), FullName = "Ehemals Betrieb", Email = "ehemals@example.com", State = UserState.Inactive }).Entity;
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR", ResponsibleUsers = [responsible, deactivated] }).Entity;
        var probe = new ChannelProbe(Answer(Delivered));
        AddReaction(Probe, ProbeSnapshot(probe), of: AddDelivery(deliveredByClient: client));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        Assert.IsNotNull(probe.Delivery);
        Assert.AreEqual(
            new ResponsibleUserInfo("Betrieb Fachschale", "betrieb@example.com"),
            Assert.ContainsSingle(probe.Delivery.Supplier.ResponsibleUsers),
            "A deactivated user no longer gets the mail meant for the client.");
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
        var reaction = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(ChannelResult.Failure(TestHelpers.Localized("The target is not reachable."))))));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var stored = await ReloadAsync(reaction);
        Assert.AreEqual(ReactionState.Pending, stored.State);
        Assert.AreEqual(dbNow.AddMinutes(1), stored.NextAttemptAt);
        Assert.AreEqual(TestHelpers.Localized("The target is not reachable."), stored.Message);
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
        Assert.AreEqual("The attempt failed unexpectedly (InvalidOperationException); the log has the details.", stored.Message?["en"], "the text of an exception can name internal paths or hosts.");
    }

    [TestMethod]
    public async Task RunNextRecordsAChannelThatCannotBeResolved()
    {
        var reaction = AddReaction("Geopilot.Pipeline.Channels.Missing", new ReactionSnapshot(new Parameterization(), []));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var stored = await ReloadAsync(reaction);
        Assert.AreEqual(ReactionState.Pending, stored.State);
        Assert.AreEqual("The channel could not be created: Channel <Geopilot.Pipeline.Channels.Missing> is unknown.", stored.Message?["en"]);
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
        Assert.AreEqual("The channel could not be created: The subfolder <../anderswo> is not a relative path without '..'. (Parameter 'subfolder')", stored.Message?["en"]);
    }

    [TestMethod]
    public async Task RunNextRecordsTheConfigOfTheAttempt()
    {
        pipelineOptions.ChannelConfigs[Probe] = new Parameterization { ["baseValue"] = "aus der Basis", ["secret"] = $"secret-{Guid.NewGuid()}" };
        AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(Delivered)), port: "587", subfolder: "kanton-a"));
        var started = CaptureStartedConfig();

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var config = started();
        Assert.IsNotNull(config);
        Assert.AreEqual(new RecordedConfigValue("aus der Basis", ConfigOrigin.Base), config["baseValue"]);
        Assert.AreEqual(new RecordedConfigValue("**********", ConfigOrigin.Base), config["secret"]);
        Assert.AreEqual(ConfigOrigin.Definition, config["port"].Origin);
        Assert.AreEqual("\"587\"", JsonSerializer.Serialize(config["port"].Value), "a value stands as configured, not converted.");
        Assert.AreEqual(ConfigOrigin.Mandate, config["subfolder"].Origin);
        recorderMock.Verify(recorder => recorder.RecordReactionAttemptFinishedAsync(It.IsAny<DeliveryReaction>(), config, ReactionState.Done, Delivered.Message), Times.Once);
    }

    [TestMethod]
    public async Task RunNextRecordsTheValueTheChannelRefuses()
    {
        var reaction = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(Delivered)), port: "-1"));
        var started = CaptureStartedConfig();

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        Assert.AreEqual("\"-1\"", JsonSerializer.Serialize(started()?["port"].Value));
        var stored = await ReloadAsync(reaction);
        Assert.AreEqual("The channel could not be created: The port <-1> is refused. (Parameter 'port')", stored.Message?["en"]);
    }

    [TestMethod]
    public async Task RunNextRecordsNoConfigForAChannelThatCannotBeResolved()
    {
        AddReaction("Geopilot.Pipeline.Channels.Missing", new ReactionSnapshot(new Parameterization(), []));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        recorderMock.Verify(recorder => recorder.RecordReactionAttemptStartedAsync(It.IsAny<DeliveryReaction>(), null), Times.Once);
    }

    [TestMethod]
    public async Task RunNextKeepsASecretOutOfEveryProtocolTable()
    {
        var secret = $"secret-{Guid.NewGuid()}";
        pipelineOptions.ChannelConfigs[Probe] = new Parameterization { ["secret"] = secret };
        AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(Delivered)), port: "-1"), reactionId: "abgelehnt", nextAttemptAt: dbNow.AddMinutes(-2));
        AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(Delivered))), reactionId: "zugestellt", nextAttemptAt: dbNow.AddMinutes(-1));
        AddRecordingOf(delivery);
        var recorder = new Recorder(context, Mock.Of<IPipelineFactory>(), Mock.Of<IUploadStorage>(), Mock.Of<IHttpContextAccessor>(), Mock.Of<ILogger<Recorder>>());

        using var worker = CreateWorker(ScopeFactory(recorder));
        await worker.RunNextAsync(CancellationToken.None);
        await worker.RunNextAsync(CancellationToken.None);

        var rows = await context.RecordingReactionEvents.AsNoTracking().Where(e => e.DeliveryId == delivery.Id).OrderBy(e => e.Id).ToListAsync();
        CollectionAssert.AreEqual(new[] { "abgelehnt", "zugestellt" }, rows.Select(row => row.ReactionId).ToArray());
        foreach (var row in rows)
        {
            Assert.IsFalse(string.IsNullOrEmpty(row.AppVersion), "the version of the instance that ran the attempt.");
            Assert.AreEqual("\"**********\"", JsonSerializer.Serialize(row.Config?["secret"].Value));
        }

        Assert.AreEqual("\"-1\"", JsonSerializer.Serialize(rows[0].Config?["port"].Value), "the refused attempt shows the value its channel refused.");
        Assert.AreEqual(0, await CountProtocolRowsContainingAsync(secret));
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
        Assert.AreEqual($"The attempt exceeded its time limit of {reactionOptions.TimeLimit}.", stored.Message?["en"]);
    }

    [TestMethod]
    public async Task RunNextCountsTheTimeBeforeTheChannelAgainstTheTimeLimit()
    {
        // The lock runs from the claim, so the time limit has to as well, or a slow database could outlast the lock.
        reactionOptions.TimeLimit = TimeSpan.FromMilliseconds(200);
        recorderMock
            .Setup(recorder => recorder.RecordReactionAttemptStartedAsync(It.IsAny<DeliveryReaction>(), It.IsAny<IReadOnlyDictionary<string, RecordedConfigValue>?>()))
            .Returns(() => Task.Delay(TimeSpan.FromMilliseconds(500)));
        var reaction = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(Delivered))));

        using var worker = CreateWorker();
        await worker.RunNextAsync(CancellationToken.None);

        var stored = await ReloadAsync(reaction);
        Assert.AreEqual(ReactionState.Pending, stored.State);
        Assert.AreEqual($"The attempt exceeded its time limit of {reactionOptions.TimeLimit}.", stored.Message?["en"]);
    }

    [TestMethod]
    public async Task RunNextLogsAnOutcomeThatCouldNotBeSaved()
    {
        // The database goes away after the channel delivered and before the attempt reports.
        AddReaction(Probe, ProbeSnapshot(new ChannelProbe(_ =>
        {
            context.Dispose();
            return Task.FromResult(Delivered);
        })));

        using var worker = CreateWorker();
        Assert.IsTrue(await worker.RunNextAsync(CancellationToken.None));

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => $"{state}".Contains("could not be saved", StringComparison.Ordinal) && $"{state}".Contains("Delivered to the target.", StringComparison.Ordinal)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        recorderMock.Verify(recorder => recorder.RecordReactionAttemptFinishedAsync(It.IsAny<DeliveryReaction>(), It.IsAny<IReadOnlyDictionary<string, RecordedConfigValue>?>(), It.IsAny<ReactionState?>(), It.IsAny<LocalizedText>()), Times.Never);
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
        Assert.AreEqual("The attempt was interrupted because the application stopped.", stored.Message?["en"]);
    }

    [TestMethod]
    public async Task RunNextGivesUpATransferAfterItsLastAttemptAndLogsAnError()
    {
        var reaction = AddReaction(Probe, ProbeSnapshot(new ChannelProbe(Answer(ChannelResult.Failure(TestHelpers.Localized("The target is not reachable."))))), attempts: 2);

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

    // Keeps the configuration the recorder is given at the start of an attempt.
    private Func<IReadOnlyDictionary<string, RecordedConfigValue>?> CaptureStartedConfig()
    {
        IReadOnlyDictionary<string, RecordedConfigValue>? started = null;
        recorderMock
            .Setup(recorder => recorder.RecordReactionAttemptStartedAsync(It.IsAny<DeliveryReaction>(), It.IsAny<IReadOnlyDictionary<string, RecordedConfigValue>?>()))
            .Callback<DeliveryReaction, IReadOnlyDictionary<string, RecordedConfigValue>?>((_, config) => started = config)
            .Returns(Task.CompletedTask);
        return () => started;
    }

    // The protocol of the reactions belongs to the recording of the run the delivery came from.
    private void AddRecordingOf(Delivery of)
    {
        context.Recordings.Add(new Recording { JobId = of.JobId, PipelineId = "ablage", Definition = "{}", AppVersion = "1.0.0", UploadStorageLocation = "uploads", UploadInitiatedAt = dbNow, StartedAt = dbNow });
        context.SaveChanges();
    }

    // Looks at whole rows, so a column added to a protocol table later is searched as well.
    private async Task<int> CountProtocolRowsContainingAsync(string text)
    {
        var protocolTypes = new[] { typeof(Recording), typeof(RecordingFile), typeof(RecordingStep), typeof(RecordingInput), typeof(RecordingCondition), typeof(RecordingArtifact), typeof(RecordingReactionEvent) };
        var count = 0;
        foreach (var table in protocolTypes.Select(type => context.Model.FindEntityType(type)?.GetTableName()).OfType<string>())
        {
            var sql = $"SELECT count(*)::int AS \"Value\" FROM \"{table}\" t WHERE t::text LIKE @pattern";
            count += await context.Database.SqlQueryRaw<int>(sql, new NpgsqlParameter("pattern", $"%{text}%")).SingleAsync();
        }

        return count;
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
            Mock.Of<IDirectoryProvider>(),
            loggerMock.Object);
    }

    // Every attempt gets the context of the test, whose transaction is rolled back afterwards.
    private IServiceScopeFactory ScopeFactory(IRecorder? recorder = null)
    {
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(provider => provider.GetService(typeof(Context))).Returns(context);
        serviceProviderMock.Setup(provider => provider.GetService(typeof(IAssetFileStore))).Returns(assetStore);
        serviceProviderMock.Setup(provider => provider.GetService(typeof(IRecorder))).Returns(recorder ?? recorderMock.Object);
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

    private Delivery AddDelivery(User? deliveredByUser = null, MachineClient? deliveredByClient = null, Delivery? precursor = null, List<DeliveredMetadataField>? metadata = null)
    {
        var newDelivery = new Delivery
        {
            JobId = Guid.NewGuid(),
            Date = dbNow.AddHours(-1),
            Mandate = new Mandate { Name = TestHelpers.Localized("Ablage Kanton A"), Key = $"ablage-{Guid.NewGuid():N}" },
            DeliveredByUser = deliveredByUser,
            DeliveredByClient = deliveredByClient,
            PrecursorDelivery = precursor,
            Partial = true,
            Metadata = metadata ?? [],
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
