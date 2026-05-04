using Geopilot.Api.Enums;
using Geopilot.Api.Pipeline;
using Moq;
using System.Collections.Immutable;

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
        Assert.IsFalse(job.IsFailed);
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
    public void MarkAsFailedSetsFlag()
    {
        var job = store.CreateJob();
        var updated = store.MarkAsFailed(job.Id);

        Assert.IsTrue(updated.IsFailed);
    }

    [TestMethod]
    public void MarkAsFailedThrowsIfJobNotFound()
    {
        Assert.ThrowsExactly<ArgumentException>(() => store.MarkAsFailed(Guid.NewGuid()));
    }

    [TestMethod]
    public void AddUploadInfoToJob()
    {
        var job = store.CreateJob();
        var cloudFiles = ImmutableList.Create(
            new CloudFileInfo("file1.xtf", "jobs/file1.xtf", 1024),
            new CloudFileInfo("file2.xtf", "jobs/file2.xtf", 2048));

        store.AddUploadInfoToJob(job.Id, UploadMethod.Cloud, cloudFiles);
        var updated = store.GetJob(job.Id);

        Assert.IsNotNull(updated);
        Assert.AreEqual(UploadMethod.Cloud, updated.UploadMethod);
        Assert.IsNotNull(updated.CloudFiles);
        Assert.HasCount(2, updated.CloudFiles);
    }

    [TestMethod]
    public void AddUploadInfoToJobThrowsIfJobNotFound()
    {
        var cloudFiles = ImmutableList.Create(new CloudFileInfo("file.xtf", "jobs/file.xtf", 1024));
        Assert.ThrowsExactly<ArgumentException>(() => store.AddUploadInfoToJob(Guid.NewGuid(), UploadMethod.Cloud, cloudFiles));
    }

    [TestMethod]
    public void AddUploadInfoToJobThrowsIfFilesAlreadyAdded()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "a", "b");

        var cloudFiles = ImmutableList.Create(new CloudFileInfo("file.xtf", "jobs/file.xtf", 1024));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.AddUploadInfoToJob(job.Id, UploadMethod.Cloud, cloudFiles));
    }

    [TestMethod]
    public void GetActiveCloudJobCountReturnsCorrectCount()
    {
        Assert.AreEqual(0, store.GetActiveCloudJobCount());

        store.CreateJob(); // Direct upload — not counted
        Assert.AreEqual(0, store.GetActiveCloudJobCount());

        var cloudJob = store.CreateJob();
        store.AddUploadInfoToJob(cloudJob.Id, UploadMethod.Cloud, ImmutableList.Create(new CloudFileInfo("f.xtf", "key", 100)));
        Assert.AreEqual(1, store.GetActiveCloudJobCount());

        var cloudJob2 = store.CreateJob();
        store.AddUploadInfoToJob(cloudJob2.Id, UploadMethod.Cloud, ImmutableList.Create(new CloudFileInfo("g.xtf", "key2", 200)));
        Assert.AreEqual(2, store.GetActiveCloudJobCount());

        store.RemoveJob(cloudJob.Id);
        Assert.AreEqual(1, store.GetActiveCloudJobCount());
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
