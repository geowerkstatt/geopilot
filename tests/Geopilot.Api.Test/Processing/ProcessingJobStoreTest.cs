using Geopilot.Pipeline;
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
    public void AddFileToJobThrowsAfterPipelineStarted()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "a", "b");
        store.StartJob(job.Id, new Mock<IPipeline>().Object, 1);

        Assert.ThrowsExactly<InvalidOperationException>(() => store.AddFileToJob(job.Id, "a2", "b2"));
    }

    [TestMethod]
    public void StartJob()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "a", "b");

        var pipeline = new Mock<IPipeline>().Object;
        var mandateId = 123;
        store.StartJob(job.Id, pipeline, mandateId);
        var updated = store.GetJob(job.Id);

        Assert.IsNotNull(updated);
        Assert.AreEqual(mandateId, updated.MandateId);
        Assert.AreSame(pipeline, updated.Pipeline);

        // The pipeline should be queued exactly once
        var queue = store.ProcessingQueue;
        var read = new List<IPipeline>();
        while (queue.TryRead(out var p))
            read.Add(p);

        Assert.HasCount(1, read);
        Assert.AreSame(pipeline, read[0]);
    }

    [TestMethod]
    public void StartJobThrowsIfJobNotFound()
    {
        var pipeline = new Mock<IPipeline>().Object;
        Assert.ThrowsExactly<ArgumentException>(() => store.StartJob(Guid.NewGuid(), pipeline, 0));
    }

    [TestMethod]
    public void StartJobThrowsIfPipelineAlreadyAssociated()
    {
        var job = store.CreateJob();
        var pipeline = new Mock<IPipeline>().Object;
        store.StartJob(job.Id, pipeline, 0);

        Assert.ThrowsExactly<InvalidOperationException>(() => store.StartJob(job.Id, new Mock<IPipeline>().Object, 0));
    }

    [TestMethod]
    public void StartJobSetsStateToRunning()
    {
        var job = store.CreateJob();
        var updated = store.StartJob(job.Id, new Mock<IPipeline>().Object, 1);

        Assert.AreEqual(ProcessingState.Running, updated.State);
    }

    [TestMethod]
    [DataRow(ProcessingState.Success)]
    [DataRow(ProcessingState.Failed)]
    [DataRow(ProcessingState.Cancelled)]
    public void PipelineFinishedTransitionsFromRunning(ProcessingState pipelineState)
    {
        var job = store.CreateJob();
        store.StartJob(job.Id, new Mock<IPipeline>().Object, 1);
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
        store.StartJob(job.Id, new Mock<IPipeline>().Object, 1);

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
        store.StartJob(job.Id, new Mock<IPipeline>().Object, 1);
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
        store.StartJob(job.Id, pipelineMock.Object, 0);

        var removed = store.RemoveJob(job.Id);

        Assert.IsTrue(removed);
        Assert.IsNull(store.GetJob(job.Id));
        pipelineMock.Verify(p => p.Dispose(), Times.Once);
    }
}
