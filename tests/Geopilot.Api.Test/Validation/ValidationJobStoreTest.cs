using Geopilot.Api.Enums;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Validation;

[TestClass]
public class ValidationJobStoreTest
{
    private Mock<IServiceScopeFactory> serviceScopeFactoryMock;
    private Mock<IFileProvider> fileProviderMock;

    private ValidationJobStore store;

    [TestInitialize]
    public void Initialize()
    {
        serviceScopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Loose);
        fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);

        store = new ValidationJobStore(serviceScopeFactoryMock.Object);

        var serviceScopeMock = new Mock<IServiceScope>(MockBehavior.Loose);
        var serviceProviderMock = new Mock<IServiceProvider>(MockBehavior.Loose);
        serviceScopeFactoryMock.Setup(x => x.CreateScope()).Returns(serviceScopeMock.Object);
        serviceScopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        serviceProviderMock.Setup(p => p.GetService(typeof(IFileProvider))).Returns(fileProviderMock.Object);
    }

    [TestMethod]
    public void CreateJob()
    {
        var job = store.CreateJob();

        Assert.IsNotNull(job);
        Assert.AreEqual(Status.Created, job.Status);
        Assert.IsNull(job.OriginalFileName);
        Assert.IsNull(job.TempFileName);
        Assert.AreNotEqual(Guid.Empty, job.Id);
        Assert.IsEmpty(job.ValidatorResults);
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
        var job = store.GetJob(Guid.NewGuid());
        Assert.IsNull(job);
    }

    [TestMethod]
    public void AddFileToJob()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "original.txt", "temp.txt");
        var updated = store.GetJob(job.Id);

        Assert.IsNotNull(updated);
        Assert.AreEqual("original.txt", updated.OriginalFileName);
        Assert.AreEqual("temp.txt", updated.TempFileName);
        Assert.AreEqual(Status.Ready, updated.Status);
    }

    [TestMethod]
    public void AddFileToJobThrowsIfJobNotFound()
    {
        Assert.ThrowsExactly<ArgumentException>(() => store.AddFileToJob(Guid.NewGuid(), "a", "b"));
    }

    [TestMethod]
    public void AddFileToJobThrowsIfStatusNotCreated()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "a", "b");
        Assert.ThrowsExactly<InvalidOperationException>(() => store.AddFileToJob(job.Id, "a2", "b2"));
    }

    [TestMethod]
    public void StartJob()
    {
        var job = store.CreateJob();
        var mandateId = 123;
        store.AddFileToJob(job.Id, "a", "b");

        var pipeline = new Mock<IPipeline>();

        store.StartJob(job.Id, pipeline.Object, mandateId);
        var updated = store.GetJob(job.Id);

        Assert.IsNotNull(updated);
        Assert.AreEqual(Status.Processing, updated.Status);
        Assert.AreEqual(mandateId, updated.MandateId);
        Assert.HasCount(1, updated.ValidatorResults);

        // Check that pipeline is in the queue once
        var queue = store.ValidationQueue;
        var readPipelines = new List<IPipeline>();
        while (queue.TryRead(out var p))
            readPipelines.Add(p);

        Assert.HasCount(1, readPipelines);
        Assert.Contains(pipeline.Object, readPipelines);
    }

    [TestMethod]
    public void StartJobThrowsIfJobNotFound()
    {
        var pipeline = new Mock<IPipeline>();
        Assert.ThrowsExactly<ArgumentException>(() => store.StartJob(Guid.NewGuid(), pipeline.Object, 0));
    }

    [TestMethod]
    public void StartJobThrowsIfStatusNotReady()
    {
        var job = store.CreateJob();
        var pipeline = new Mock<IPipeline>();
        Assert.ThrowsExactly<InvalidOperationException>(() => store.StartJob(job.Id, pipeline.Object, 0));
    }

    [TestMethod]
    public void AddValidatorResult()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "original.xtf", "temp.xtf");

        fileProviderMock.Setup(p => p.Initialize(job.Id));
        fileProviderMock.Setup(p => p.CreateFileWithRandomName(".xtf"));

        var pipeline = new Mock<IPipeline>();
        store.StartJob(job.Id, pipeline.Object, 0);

        var result = new ValidatorResult(ValidatorResultStatus.Completed, "some message");
        store.AddValidatorResult(pipeline.Object, result);
        var updated = store.GetJob(job.Id);

        Assert.IsNotNull(updated);
        Assert.AreEqual(Status.Completed, updated.Status);
        Assert.AreEqual(result.StatusMessage, updated.ValidatorResults["INTERLIS"]?.StatusMessage);
    }

    [TestMethod]
    public void SetJobStatus()
    {
        var job = store.CreateJob();
        var updated = store.SetJobStatus(job.Id, Status.VerifyingUpload);

        Assert.AreEqual(Status.VerifyingUpload, updated.Status);
        Assert.AreEqual(job.Id, updated.Id);
    }

    [TestMethod]
    public void SetJobStatusThrowsIfJobNotFound()
    {
        Assert.ThrowsExactly<ArgumentException>(() => store.SetJobStatus(Guid.NewGuid(), Status.VerifyingUpload));
    }

    [TestMethod]
    public void AddFileToJobSucceedsForVerifyingUploadStatus()
    {
        var job = store.CreateJob();
        store.SetJobStatus(job.Id, Status.VerifyingUpload);

        var updated = store.AddFileToJob(job.Id, "original.txt", "temp.txt");

        Assert.AreEqual("original.txt", updated.OriginalFileName);
        Assert.AreEqual("temp.txt", updated.TempFileName);
        Assert.AreEqual(Status.Ready, updated.Status);
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
        Assert.AreEqual(Status.Created, updated.Status);
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
    public void GetActiveCloudJobCountReturnsCorrectCount()
    {
        Assert.AreEqual(0, store.GetActiveCloudJobCount());

        // Direct upload job should not be counted
        store.CreateJob();
        Assert.AreEqual(0, store.GetActiveCloudJobCount());

        // Cloud upload job should be counted
        var cloudJob = store.CreateJob();
        store.AddUploadInfoToJob(cloudJob.Id, UploadMethod.Cloud, ImmutableList.Create(new CloudFileInfo("f.xtf", "key", 100)));
        Assert.AreEqual(1, store.GetActiveCloudJobCount());

        // Second cloud upload job
        var cloudJob2 = store.CreateJob();
        store.AddUploadInfoToJob(cloudJob2.Id, UploadMethod.Cloud, ImmutableList.Create(new CloudFileInfo("g.xtf", "key2", 200)));
        Assert.AreEqual(2, store.GetActiveCloudJobCount());

        // Removing a cloud job decreases count
        store.RemoveJob(cloudJob.Id);
        Assert.AreEqual(1, store.GetActiveCloudJobCount());
    }

    [TestMethod]
    public void AddUploadInfoToJobThrowsIfStatusNotCreated()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "a", "b");

        var cloudFiles = ImmutableList.Create(new CloudFileInfo("file.xtf", "jobs/file.xtf", 1024));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.AddUploadInfoToJob(job.Id, UploadMethod.Cloud, cloudFiles));
    }
}
