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
        var job = store.CreateJob();

        Assert.IsNotNull(job);
        Assert.HasCount(0, job.Files);
        Assert.AreNotEqual(Guid.Empty, job.Id);
        Assert.IsNull(job.Pipeline);
        Assert.AreEqual(ProcessingState.Pending, job.State);
    }

    [TestMethod]
    public void GetJob()
    {
        var created = store.CreateJob();
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
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "original.txt", "temp.txt");
        var updated = store.GetJob(job.Id);

        Assert.IsNotNull(updated);
        Assert.HasCount(1, updated.Files);
        Assert.AreEqual("original.txt", updated.Files[0].OriginalFileName);
        Assert.AreEqual("temp.txt", updated.Files[0].TempFileName);
    }

    [TestMethod]
    public void AddFileToJobThrowsIfJobNotFound()
    {
        Assert.ThrowsExactly<ArgumentException>(() => store.AddFileToJob(Guid.NewGuid(), "a", "b"));
    }

    [TestMethod]
    public void AddFileToJobSucceedsWhileStillPendingAfterPipelineAttached()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "a", "b");
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);

        var updated = store.AddFileToJob(job.Id, "a2", "b2");

        Assert.HasCount(2, updated.Files);
    }

    [TestMethod]
    public void AddFileToJobThrowsAfterEnqueued()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "a", "b");
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, new PipelineFileList());

        Assert.ThrowsExactly<InvalidOperationException>(() => store.AddFileToJob(job.Id, "a2", "b2"));
    }

    [TestMethod]
    public void AttachPipeline()
    {
        var job = store.CreateJob();

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
        var job = store.CreateJob();
        var pipeline = new Mock<IPipeline>().Object;
        store.AttachPipeline(job.Id, pipeline, 0);

        Assert.ThrowsExactly<InvalidOperationException>(() => store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 0));
    }

    [TestMethod]
    public void AttachPipelineThrowsIfJobFailed()
    {
        var job = store.CreateJob();
        store.MarkAsFailed(job.Id);

        Assert.ThrowsExactly<InvalidOperationException>(() => store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 0));
    }

    [TestMethod]
    public void EnqueueForProcessingQueuesPipelineWithFiles()
    {
        var job = store.CreateJob();
        var pipeline = new Mock<IPipeline>().Object;
        store.AttachPipeline(job.Id, pipeline, 1);

        var files = new PipelineFileList();
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
        var job = store.CreateJob();
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);

        var updated = store.EnqueueForProcessing(job.Id, new PipelineFileList());

        Assert.AreEqual(ProcessingState.Running, updated.State);
    }

    [TestMethod]
    public void EnqueueForProcessingThrowsIfNoPipelineAttached()
    {
        var job = store.CreateJob();

        Assert.ThrowsExactly<InvalidOperationException>(() => store.EnqueueForProcessing(job.Id, new PipelineFileList()));
    }

    [TestMethod]
    public void EnqueueForProcessingThrowsIfNotPending()
    {
        var job = store.CreateJob();
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, new PipelineFileList());

        Assert.ThrowsExactly<InvalidOperationException>(() => store.EnqueueForProcessing(job.Id, new PipelineFileList()));
    }

    [TestMethod]
    [DataRow(ProcessingState.Success)]
    [DataRow(ProcessingState.Failed)]
    [DataRow(ProcessingState.Cancelled)]
    public void PipelineFinishedTransitionsFromRunning(ProcessingState pipelineState)
    {
        var job = store.CreateJob();
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, new PipelineFileList());
        var updated = store.PipelineFinished(job.Id, pipelineState);

        Assert.AreEqual(pipelineState, updated.State);
    }

    [TestMethod]
    public void PipelineFinishedThrowsIfNotRunning()
    {
        var job = store.CreateJob();

        Assert.ThrowsExactly<InvalidOperationException>(() => store.PipelineFinished(job.Id, ProcessingState.Success));
    }

    [TestMethod]
    [DataRow(ProcessingState.Pending)]
    [DataRow(ProcessingState.Running)]
    public void PipelineFinishedThrowsIfPipelineStateIsNotTerminal(ProcessingState pipelineState)
    {
        var job = store.CreateJob();
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, new PipelineFileList());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.PipelineFinished(job.Id, pipelineState));
    }

    [TestMethod]
    public void MarkAsFailedSetsState()
    {
        var job = store.CreateJob();
        var updated = store.MarkAsFailed(job.Id);

        Assert.AreEqual(ProcessingState.Failed, updated.State);
    }

    [TestMethod]
    public void MarkAsFailedThrowsIfAlreadyTerminal()
    {
        var job = store.CreateJob();
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, new PipelineFileList());
        store.PipelineFinished(job.Id, ProcessingState.Success);

        Assert.ThrowsExactly<InvalidOperationException>(() => store.MarkAsFailed(job.Id));
    }

    [TestMethod]
    public void MarkAsFailedThrowsIfJobNotFound()
    {
        Assert.ThrowsExactly<ArgumentException>(() => store.MarkAsFailed(Guid.NewGuid()));
    }

    [TestMethod]
    public void RemoveJobDisposesPipeline()
    {
        var job = store.CreateJob();
        var pipelineMock = new Mock<IPipeline>();
        store.AttachPipeline(job.Id, pipelineMock.Object, 0);

        var removed = store.RemoveJob(job.Id);

        Assert.IsTrue(removed);
        Assert.IsNull(store.GetJob(job.Id));
        pipelineMock.Verify(p => p.Dispose(), Times.Once);
    }
}
