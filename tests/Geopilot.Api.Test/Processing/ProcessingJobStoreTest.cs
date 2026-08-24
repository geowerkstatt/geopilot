using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Moq;

namespace Geopilot.Api.Processing;

[TestClass]
public class ProcessingJobStoreTest
{
    private ProcessingJobStore store;

    [TestInitialize]
    public void Initialize()
    {
        store = new ProcessingJobStore();
    }

    [TestMethod]
    public void CreateJob()
    {
        var job = store.CreateJob(Guid.NewGuid());

        Assert.IsNotNull(job);
        Assert.HasCount(0, job.Files);
        Assert.AreNotEqual(Guid.Empty, job.Id);
        Assert.IsNull(job.Pipeline);
        Assert.AreEqual(ProcessingState.Pending, job.State);
    }

    [TestMethod]
    public void CreateJobSetsUtcCreatedAt()
    {
        var before = DateTime.UtcNow;
        var job = store.CreateJob(Guid.NewGuid());
        var after = DateTime.UtcNow;

        Assert.AreEqual(DateTimeKind.Utc, job.CreatedAt.Kind);
        Assert.IsTrue(before <= job.CreatedAt);
        Assert.IsTrue(job.CreatedAt <= after);
    }

    [TestMethod]
    public void GetJob()
    {
        var created = store.CreateJob(Guid.NewGuid());
        var fetched = store.GetJob(created.Id);

        Assert.IsNotNull(fetched);
        Assert.AreEqual(created.Id, fetched.Id);
    }

    [TestMethod]
    public void GetJobReturnsNullIfNotFound()
    {
        Assert.IsNull(store.GetJob(Guid.NewGuid()));
    }

    [TestMethod]
    public void AddFileToJob()
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AddFileToJob(job.Id, "original.txt", "temp.txt", "uploads/upload/" + "original.txt");
        var updated = store.GetJob(job.Id);

        Assert.IsNotNull(updated);
        Assert.HasCount(1, updated.Files);
        Assert.AreEqual("original.txt", updated.Files[0].OriginalFileName);
        Assert.AreEqual("temp.txt", updated.Files[0].TempFileName);
    }

    [TestMethod]
    public void AddFileToJobUnderConcurrencyAddsEachFileExactlyOnce()
    {
        const int fileCount = 100;
        var job = store.CreateJob(Guid.NewGuid());

        Parallel.For(0, fileCount, i => store.AddFileToJob(job.Id, $"file_{i}.xtf", $"temp_{i}.xtf", $"uploads/key/file_{i}.xtf"));

        // AddOrUpdate re-runs its update factory whenever the compare-and-swap loses a race, so a factory
        // with a side effect drops or duplicates files exactly here.
        var files = store.GetJob(job.Id)!.Files;
        Assert.HasCount(fileCount, files);
        Assert.HasCount(fileCount, files.Select(f => f.OriginalFileName).Distinct().ToList(), "No file may be added twice.");
    }

    [TestMethod]
    public void AddFileToJobThrowsIfJobNotFound()
    {
        Assert.ThrowsExactly<ArgumentException>(() => store.AddFileToJob(Guid.NewGuid(), "a", "b", "uploads/upload/a"));
    }

    [TestMethod]
    public void AddFileToJobSucceedsWhileStillPendingAfterPipelineAttached()
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AddFileToJob(job.Id, "a", "b", "uploads/upload/" + "a");
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);

        var updated = store.AddFileToJob(job.Id, "a2", "b2", "uploads/upload/" + "a2");

        Assert.HasCount(2, updated.Files);
    }

    [TestMethod]
    public void AddFileToJobThrowsAfterEnqueued()
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AddFileToJob(job.Id, "a", "b", "uploads/upload/" + "a");
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>());

        Assert.ThrowsExactly<InvalidOperationException>(() => store.AddFileToJob(job.Id, "a2", "b2", "uploads/upload/" + "a2"));
    }

    [TestMethod]
    public void AttachPipeline()
    {
        var job = store.CreateJob(Guid.NewGuid());

        var pipeline = new Mock<IPipeline>().Object;
        var mandateId = 123;
        store.AttachPipeline(job.Id, pipeline, mandateId);
        var updated = store.GetJob(job.Id);

        Assert.IsNotNull(updated);
        Assert.AreEqual(mandateId, updated.MandateId);
        Assert.AreSame(pipeline, updated.Pipeline);

        // Attaching alone must not queue the pipeline and must leave the job pending.
        Assert.AreEqual(ProcessingState.Pending, updated.State);
        Assert.IsFalse(store.ProcessingQueue.TryRead(out _));
    }

    [TestMethod]
    public void AttachPipelineThrowsIfJobNotFound()
    {
        var pipeline = new Mock<IPipeline>().Object;
        Assert.ThrowsExactly<ArgumentException>(() => store.AttachPipeline(Guid.NewGuid(), pipeline, 0));
    }

    [TestMethod]
    public void AttachPipelineThrowsIfPipelineAlreadyAssociated()
    {
        var job = store.CreateJob(Guid.NewGuid());
        var pipeline = new Mock<IPipeline>().Object;
        store.AttachPipeline(job.Id, pipeline, 0);

        Assert.ThrowsExactly<InvalidOperationException>(() => store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 0));
    }

    [TestMethod]
    public void AttachPipelineThrowsIfJobFailed()
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.MarkAsFailed(job.Id);

        Assert.ThrowsExactly<InvalidOperationException>(() => store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 0));
    }

    [TestMethod]
    public void EnqueueForProcessingQueuesPipelineWithFiles()
    {
        var job = store.CreateJob(Guid.NewGuid());
        var pipeline = new Mock<IPipeline>().Object;
        store.AttachPipeline(job.Id, pipeline, 1);

        var files = Array.Empty<IPipelineFile>();
        store.EnqueueForProcessing(job.Id, files);

        // The work item should be queued exactly once and carry the attached pipeline and the staged files.
        var queue = store.ProcessingQueue;
        var read = new List<ProcessingWorkItem>();
        while (queue.TryRead(out var item))
            read.Add(item);

        Assert.HasCount(1, read);
        Assert.AreSame(pipeline, read[0].Pipeline);
        Assert.AreSame(files, read[0].Files);
    }

    [TestMethod]
    public void EnqueueForProcessingSetsStateToRunning()
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);

        var updated = store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>());

        Assert.AreEqual(ProcessingState.Running, updated.State);
    }

    [TestMethod]
    public void EnqueueForProcessingThrowsIfNoPipelineAttached()
    {
        var job = store.CreateJob(Guid.NewGuid());

        Assert.ThrowsExactly<InvalidOperationException>(() => store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>()));
    }

    [TestMethod]
    public void EnqueueForProcessingThrowsIfNotPending()
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>());

        Assert.ThrowsExactly<InvalidOperationException>(() => store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>()));
    }

    [TestMethod]
    [DataRow(ProcessingState.Success)]
    [DataRow(ProcessingState.Warning)]
    [DataRow(ProcessingState.DeliveryRestriction)]
    [DataRow(ProcessingState.Failed)]
    [DataRow(ProcessingState.Cancelled)]
    public void PipelineFinishedTransitionsFromRunning(ProcessingState pipelineState)
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>());
        var updated = store.PipelineFinished(job.Id, pipelineState);

        Assert.AreEqual(pipelineState, updated.State);
    }

    [TestMethod]
    public void PipelineFinishedThrowsIfNotRunning()
    {
        var job = store.CreateJob(Guid.NewGuid());

        Assert.ThrowsExactly<InvalidOperationException>(() => store.PipelineFinished(job.Id, ProcessingState.Success));
    }

    [TestMethod]
    [DataRow(ProcessingState.Pending)]
    [DataRow(ProcessingState.Running)]
    public void PipelineFinishedThrowsIfPipelineStateIsNotTerminal(ProcessingState pipelineState)
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.PipelineFinished(job.Id, pipelineState));
    }

    [TestMethod]
    public void MarkAsFailedSetsState()
    {
        var job = store.CreateJob(Guid.NewGuid());
        var updated = store.MarkAsFailed(job.Id);

        Assert.AreEqual(ProcessingState.Failed, updated.State);
    }

    [TestMethod]
    public void MarkAsFailedThrowsIfAlreadyTerminal()
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>());
        store.PipelineFinished(job.Id, ProcessingState.Success);

        Assert.ThrowsExactly<InvalidOperationException>(() => store.MarkAsFailed(job.Id));
    }

    [TestMethod]
    public void MarkAsFailedThrowsIfJobNotFound()
    {
        Assert.ThrowsExactly<ArgumentException>(() => store.MarkAsFailed(Guid.NewGuid()));
    }

    [TestMethod]
    public void TryMarkAsFailedSetsState()
    {
        var job = store.CreateJob(Guid.NewGuid());

        Assert.IsTrue(store.TryMarkAsFailed(job.Id));
        Assert.AreEqual(ProcessingState.Failed, store.GetJob(job.Id)!.State);
    }

    [TestMethod]
    public void TryMarkAsFailedReturnsFalseIfAlreadyTerminal()
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>());
        store.PipelineFinished(job.Id, ProcessingState.Success);

        Assert.IsFalse(store.TryMarkAsFailed(job.Id));
        Assert.AreEqual(ProcessingState.Success, store.GetJob(job.Id)!.State, "A rejected transition must leave the job untouched.");
    }

    [TestMethod]
    public void TryMarkAsFailedReturnsFalseIfJobNotFound()
    {
        Assert.IsFalse(store.TryMarkAsFailed(Guid.NewGuid()));
    }

    [TestMethod]
    [DataRow(ProcessingState.Success)]
    [DataRow(ProcessingState.Warning)]
    [DataRow(ProcessingState.DeliveryRestriction)]
    [DataRow(ProcessingState.Failed)]
    [DataRow(ProcessingState.Cancelled)]
    public void TryPipelineFinishedTransitionsFromRunning(ProcessingState pipelineState)
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>());

        Assert.IsTrue(store.TryPipelineFinished(job.Id, pipelineState));
        Assert.AreEqual(pipelineState, store.GetJob(job.Id)!.State);
    }

    [TestMethod]
    public void TryPipelineFinishedReturnsFalseIfNotRunning()
    {
        var job = store.CreateJob(Guid.NewGuid());

        Assert.IsFalse(store.TryPipelineFinished(job.Id, ProcessingState.Success));
        Assert.AreEqual(ProcessingState.Pending, store.GetJob(job.Id)!.State, "A rejected transition must leave the job untouched.");
    }

    [TestMethod]
    [DataRow(ProcessingState.Pending)]
    [DataRow(ProcessingState.Running)]
    public void TryPipelineFinishedReturnsFalseIfPipelineStateIsNotTerminal(ProcessingState pipelineState)
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>());

        Assert.IsFalse(store.TryPipelineFinished(job.Id, pipelineState));
        Assert.AreEqual(ProcessingState.Running, store.GetJob(job.Id)!.State);
    }

    [TestMethod]
    public void TryPipelineFinishedReturnsFalseIfJobNotFound()
    {
        Assert.IsFalse(store.TryPipelineFinished(Guid.NewGuid(), ProcessingState.Success));
    }

    [TestMethod]
    public void RemoveJobDisposesPipeline()
    {
        var job = store.CreateJob(Guid.NewGuid());
        var pipelineMock = new Mock<IPipeline>();
        store.AttachPipeline(job.Id, pipelineMock.Object, 0);

        var removed = store.RemoveJob(job.Id);

        Assert.IsTrue(removed);
        Assert.IsNull(store.GetJob(job.Id));
        pipelineMock.Verify(p => p.Dispose(), Times.Once);
    }
}
