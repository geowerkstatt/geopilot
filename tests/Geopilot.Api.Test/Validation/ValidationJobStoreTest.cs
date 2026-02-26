using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Moq;

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
}
