using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using System.Collections.Immutable;
using System.Data.Common;
using System.Threading.Channels;

namespace Geopilot.Api.Test.Processing;

[TestClass]
public class RecordingHeartbeatServiceTest
{
    private const string PipelineId = "pipe_a";

    private FlakyDatabase database;
    private Context context;
    private ServiceProvider services;
    private JobLifecycle lifecycle;
    private FakeTimeProvider time;
    private Mandate mandate;

    [TestInitialize]
    public void Initialize()
    {
        database = new FlakyDatabase();
        context = AssemblyInitialize.DbFixture.GetTestContext(database);
        mandate = new Mandate { Name = TestHelpers.Localized(nameof(RecordingHeartbeatServiceTest)), FileTypes = [".xtf"], PipelineId = PipelineId };
        context.Mandates.Add(mandate);
        context.SaveChanges();

        var pipelineFactory = new Mock<IPipelineFactory>();
        pipelineFactory.Setup(f => f.GetDefinitionSnapshotJson(PipelineId)).Returns("{}");
        pipelineFactory
            .Setup(f => f.CreatePipeline(PipelineId, It.IsAny<Guid>()))
            .Returns((string _, Guid jobId) => Mock.Of<IPipeline>(p => p.Id == PipelineId && p.JobId == jobId && p.Steps == new List<IPipelineStep>()));

        var uploadStorage = Mock.Of<IUploadStorage>(s => s.StorageLocation == "https://storage.example.com/uploads");
        var recorder = new Recorder(context, pipelineFactory.Object, uploadStorage, Mock.Of<IHttpContextAccessor>(), Mock.Of<ILogger<Recorder>>());

        // Every scope hands out the context of the test, so the heartbeat writes inside its transaction.
        services = new ServiceCollection()
            .AddSingleton(context)
            .AddSingleton<IRecorder>(recorder)
            .BuildServiceProvider();

        lifecycle = new JobLifecycle(
            Mock.Of<ILogger<JobLifecycle>>(),
            new ProcessingJobStore(),
            pipelineFactory.Object,
            Channel.CreateUnbounded<PreflightRequest>().Writer,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ProcessingOptions { JobTimeout = TimeSpan.FromMinutes(5) }),
            Options.Create(new RefMappingOptions()));

        time = new FakeTimeProvider();
    }

    [TestCleanup]
    public void Cleanup()
    {
        services.Dispose();
        context.Dispose();
    }

    /// <summary>
    /// The monotonic clock of the instance, moved on by the test. The database clock stands still within the
    /// transaction of a test, so the tests move a heartbeat back instead of the database clock forward.
    /// </summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan delta) => timestamp += delta.Ticks;
    }

    /// <summary>
    /// Stands in for the database: counts the queries that reach it and, while <see cref="Down"/>, fails them before
    /// they run, as a lost connection would, so the transaction of the test stays usable.
    /// </summary>
    private sealed class FlakyDatabase : DbCommandInterceptor
    {
        public bool Down { get; set; }

        public int Queries { get; private set; }

        // Runs while a query reaches the database, e.g. to let time pass during it.
        public Action? DuringQuery { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Queries++;
            DuringQuery?.Invoke();
            if (Down)
                throw new NpgsqlException("The connection to the database was lost.", new TimeoutException());

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [TestMethod]
    public async Task BeatRefreshesALivingRun()
    {
        using var service = CreateService();
        var jobId = await StartJobAsync();
        await SetHeartbeatAgeAsync(jobId, TimeSpan.FromMinutes(1));

        var refreshed = await service.BeatAsync([jobId], CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { jobId }, refreshed.ToArray());
        Assert.IsTrue(await HeartbeatIsNowAsync(jobId), "The heartbeat is written with the database clock.");
        Assert.IsTrue(await IsAliveAsync(jobId));
    }

    [TestMethod]
    public async Task BeatDoesNotReviveADeadRun()
    {
        using var service = CreateService();
        var deadJobId = await StartJobAsync();
        var livingJobId = await StartJobAsync();
        await SetHeartbeatAgeAsync(deadJobId, TimeSpan.FromMinutes(4));

        var refreshed = await service.BeatAsync([deadJobId, livingJobId], CancellationToken.None);
        await service.TickAsync(CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { livingJobId }, refreshed.ToArray(), "A run the database considers dead is not refreshed.");
        Assert.IsFalse(await IsAliveAsync(deadJobId));
        Assert.IsTrue(lifecycle.AbortTokenOf(deadJobId).IsCancellationRequested, "Nobody may go on working on a dead run.");
        Assert.AreEqual("heartbeat lost", lifecycle.AbortReasonOf(deadJobId));
        Assert.IsFalse(lifecycle.AbortTokenOf(livingJobId).IsCancellationRequested, "Only the job whose run died is aborted.");
    }

    [TestMethod]
    public async Task LongStepStaysAlive()
    {
        using var service = CreateService();
        var jobId = await StartJobAsync();

        // The job stays held while its step computes, here over two ticks two minutes apart, together longer than
        // Liveness.DeadAfter. By the database clock, each tick finds the last heartbeat two minutes old.
        await SetHeartbeatAgeAsync(jobId, TimeSpan.FromMinutes(2));
        time.Advance(TimeSpan.FromMinutes(2));
        await service.TickAsync(CancellationToken.None);
        Assert.IsTrue(await HeartbeatIsNowAsync(jobId), "The first tick refreshes the heartbeat.");

        await SetHeartbeatAgeAsync(jobId, TimeSpan.FromMinutes(2));
        time.Advance(TimeSpan.FromMinutes(2));
        await service.TickAsync(CancellationToken.None);
        Assert.IsTrue(await HeartbeatIsNowAsync(jobId), "The second tick refreshes the heartbeat.");

        Assert.IsTrue(await IsAliveAsync(jobId));
        Assert.IsFalse(lifecycle.AbortTokenOf(jobId).IsCancellationRequested);
    }

    [TestMethod]
    public async Task LostDatabaseAbortsHeldJobsAfterTheLimit()
    {
        using var service = CreateService();
        var jobIds = new[] { await StartJobAsync(), await StartJobAsync() };
        await service.TickAsync(CancellationToken.None);
        database.Down = true;

        // The database fails fast, so each failure comes a few ms short of the 30 s grid of the ticks. One interval
        // before the last tick within Liveness.AbortAfter, the jobs go on.
        var fewMilliseconds = TimeSpan.FromMilliseconds(5);
        time.Advance(Liveness.AbortAfter - Liveness.HeartbeatInterval - fewMilliseconds);
        await service.TickAsync(CancellationToken.None);
        Assert.IsFalse(jobIds.Any(jobId => lifecycle.AbortTokenOf(jobId).IsCancellationRequested), "The next tick still comes within the limit.");

        time.Advance(Liveness.HeartbeatInterval);
        await service.TickAsync(CancellationToken.None);

        foreach (var jobId in jobIds)
        {
            Assert.IsTrue(lifecycle.AbortTokenOf(jobId).IsCancellationRequested, "The next tick would come after the limit, when the others may already consider the runs dead.");
            Assert.AreEqual("database unreachable", lifecycle.AbortReasonOf(jobId));
        }
    }

    [TestMethod]
    public async Task LastSuccessCountsFromTheStartOfItsStatement()
    {
        using var service = CreateService();
        var jobId = await StartJobAsync();

        // The last successful statement takes ten seconds. It wrote the heartbeat after its start, so measuring from
        // there is never late.
        database.DuringQuery = () => time.Advance(TimeSpan.FromSeconds(10));
        await service.TickAsync(CancellationToken.None);
        database.DuringQuery = null;
        database.Down = true;

        // Two minutes and a few ms after the start of that statement, the next tick would come after the limit.
        time.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromMilliseconds(5) - TimeSpan.FromSeconds(10));
        await service.TickAsync(CancellationToken.None);

        Assert.IsTrue(lifecycle.AbortTokenOf(jobId).IsCancellationRequested, "Counted from the end of the statement, the job would only stop after the limit.");
    }

    [TestMethod]
    public async Task IdleInstanceWritesNothingAndRestartsTheMeasurement()
    {
        using var service = CreateService();
        var queries = database.Queries;

        time.Advance(TimeSpan.FromMinutes(2));
        await service.TickAsync(CancellationToken.None);
        Assert.AreEqual(queries, database.Queries, "Without held jobs the heartbeat writes nothing.");

        var jobId = await StartJobAsync();
        database.Down = true;
        time.Advance(TimeSpan.FromMinutes(1));
        await service.TickAsync(CancellationToken.None);

        Assert.IsFalse(lifecycle.AbortTokenOf(jobId).IsCancellationRequested, "The time without held jobs does not count against the limit.");
    }

    private RecordingHeartbeatService CreateService() =>
        new(lifecycle, services.GetRequiredService<IServiceScopeFactory>(), time, Mock.Of<ILogger<RecordingHeartbeatService>>());

    /// <summary>
    /// Starts a job through the lifecycle, which holds it from then on, with the start record written to the database.
    /// </summary>
    private async Task<Guid> StartJobAsync()
    {
        var upload = new UploadInfo(Guid.NewGuid(), ImmutableList<UploadedFileInfo>.Empty, DateTime.UtcNow);
        var job = await lifecycle.StartAsync(upload, mandate, PipelineId, supplier: null, mayDeliver: false, deliveryFields: null);
        return job.Id;
    }

    // Moves the last heartbeat back by the database clock, as if that much time had passed since it was written.
    private async Task SetHeartbeatAgeAsync(Guid jobId, TimeSpan age) =>
        await context.Recordings
            .Where(r => r.JobId == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.HeartbeatAt, r => DateTime.UtcNow - age));

    // Whether the last heartbeat is the current time of the database clock, so it was just written.
    private Task<bool> HeartbeatIsNowAsync(Guid jobId) =>
        context.Recordings.AnyAsync(r => r.JobId == jobId && r.HeartbeatAt == DateTime.UtcNow);

    private Task<bool> IsAliveAsync(Guid jobId) =>
        context.Recordings.WhereAlive(context.Deliveries.IgnoreQueryFilters()).AnyAsync(r => r.JobId == jobId);
}
