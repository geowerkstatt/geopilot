using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;
using Microsoft.EntityFrameworkCore;

namespace Geopilot.Api.Test.Processing;

[TestClass]
public class LivenessTest
{
    private Context context;
    private Mandate mandate;
    private User user;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        mandate = new Mandate { Name = TestHelpers.Localized(nameof(LivenessTest)), FileTypes = [".xtf"], PipelineId = "pipe_a" };
        user = new User { FullName = nameof(LivenessTest), AuthIdentifier = Guid.NewGuid().ToString() };
        context.Mandates.Add(mandate);
        context.Users.Add(user);
        context.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup() => context.Dispose();

    // Heartbeat in minutes before the database clock (null: never beaten), delivery: 0 none, 1 present, 2 deleted.
    [TestMethod]
    [DataRow(ProcessingState.Success, null, null, 0, false, true, DisplayName = "settled without submission attempt is not alive")]
    [DataRow(null, 0, null, 0, true, true, DisplayName = "no terminal state and fresh heartbeat is alive")]
    [DataRow(null, 4, null, 0, false, false, DisplayName = "no terminal state and old heartbeat is dead")]
    [DataRow(null, null, null, 0, false, false, DisplayName = "no terminal state and no heartbeat is dead")]
    [DataRow(ProcessingState.Success, 4, "key", 0, false, false, DisplayName = "submission attempt pending with old heartbeat is dead")]
    [DataRow(ProcessingState.Success, 4, "key", 1, false, true, DisplayName = "submission attempt with delivery is settled")]
    [DataRow(ProcessingState.Success, 4, "key", 2, false, true, DisplayName = "submission attempt with deleted delivery is settled")]
    public async Task AliveAndAnswerableFollowTheDatabaseClock(
        ProcessingState? terminal, int? heartbeatMinutesAgo, string? submissionKey, int delivery, bool alive, bool answerable)
    {
        var run = await AddRunAsync(terminal, submissionKey, terminalHoursAgo: 0);
        await SetHeartbeatAsync(run, heartbeatMinutesAgo);
        if (delivery > 0)
        {
            context.Deliveries.Add(new Delivery { JobId = run.JobId, Mandate = mandate, DeliveredByUser = user, Deleted = delivery == 2 });
            await context.SaveChangesAsync();
        }

        var deliveries = context.Deliveries.IgnoreQueryFilters();
        Assert.AreEqual(alive, await context.Recordings.WhereAlive(deliveries).AnyAsync(r => r.Id == run.Id));
        Assert.AreEqual(answerable, await context.Recordings.WhereAnswerable(deliveries, TimeSpan.FromDays(1)).AnyAsync(r => r.Id == run.Id));
    }

    [TestMethod]
    [DataRow(23, true)]
    [DataRow(25, false)]
    public async Task ASettledRunAnswersOnlyWithinJobRetention(int terminalHoursAgo, bool answerable)
    {
        var run = await AddRunAsync(ProcessingState.Success, submissionKey: null, terminalHoursAgo);

        var found = await context.Recordings
            .WhereAnswerable(context.Deliveries.IgnoreQueryFilters(), TimeSpan.FromDays(1))
            .AnyAsync(r => r.Id == run.Id);

        Assert.AreEqual(answerable, found);
    }

    private async Task<Recording> AddRunAsync(ProcessingState? terminal, string? submissionKey, int terminalHoursAgo)
    {
        var run = new Recording
        {
            JobId = Guid.NewGuid(),
            PipelineId = "pipe_a",
            Definition = "{}",
            AppVersion = "test",
            MandateId = mandate.Id,
            UploadId = Guid.NewGuid(),
            UploadStorageLocation = "https://storage.example.com/uploads",
            SubmissionMandateKey = submissionKey,
            TerminalState = terminal,
        };
        context.Recordings.Add(run);
        await context.SaveChangesAsync();
        if (terminal != null)
        {
            await context.Recordings.Where(r => r.Id == run.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.TerminalAt, r => DateTime.UtcNow - TimeSpan.FromHours(terminalHoursAgo)));
        }

        return run;
    }

    private async Task SetHeartbeatAsync(Recording run, int? minutesAgo)
    {
        if (minutesAgo is null)
        {
            return;
        }

        var age = TimeSpan.FromMinutes(minutesAgo.Value);
        await context.Recordings.Where(r => r.Id == run.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.HeartbeatAt, r => DateTime.UtcNow - age));
    }
}
