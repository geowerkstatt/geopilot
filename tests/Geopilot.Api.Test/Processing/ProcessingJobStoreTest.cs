using Geopilot.Api.Exceptions;
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
    public void CreateJobAcceptsAnotherUploadWhileOneIsActive()
    {
        store.CreateJob(Guid.NewGuid());

        var job = store.CreateJob(Guid.NewGuid());

        Assert.AreEqual(ProcessingState.Pending, job.State);
    }

    [TestMethod]
    public void CreateJobAdmitsOneOfConcurrentStartsForTheSameUpload()
    {
        var uploadId = Guid.NewGuid();
        var admitted = 0;

        Parallel.For(0, 16, _ =>
        {
            try
            {
                store.CreateJob(uploadId);
                Interlocked.Increment(ref admitted);
            }
            catch (JobAlreadyActiveException)
            {
                // The expected answer for every start but one.
            }
        });

        Assert.AreEqual(1, admitted);
        Assert.HasCount(1, store.GetJobIds());
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
        store.TryMarkAsFailed(job.Id);

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
    public void TryMarkAsFailedReturnsFalseIfAlreadyTerminal()
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>());
        Assert.IsTrue(store.TryPipelineFinished(job.Id, ProcessingState.Success));

        Assert.IsFalse(store.TryMarkAsFailed(job.Id));
        Assert.AreEqual(ProcessingState.Success, store.GetJob(job.Id)!.State, "A rejected transition must leave the job untouched.");
    }

    [TestMethod]
    public void TryMarkAsFailedReturnsFalseIfJobNotFound()
    {
        Assert.IsFalse(store.TryMarkAsFailed(Guid.NewGuid()));
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
    public void TryPipelineFinishedThrowsIfPipelineStateIsNotTerminal(ProcessingState pipelineState)
    {
        var job = store.CreateJob(Guid.NewGuid());
        store.AttachPipeline(job.Id, new Mock<IPipeline>().Object, 1);
        store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.TryPipelineFinished(job.Id, pipelineState));
        Assert.AreEqual(ProcessingState.Running, store.GetJob(job.Id)!.State);
    }

    [TestMethod]
    public void TryPipelineFinishedReturnsFalseIfJobNotFound()
    {
        Assert.IsFalse(store.TryPipelineFinished(Guid.NewGuid(), ProcessingState.Success));
    }
}
