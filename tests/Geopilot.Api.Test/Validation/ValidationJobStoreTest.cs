using System.Collections.Immutable;
using Geopilot.Api.Enums;
using Moq;

namespace Geopilot.Api.Validation;

[TestClass]
public class ValidationJobStoreTest
{
    private ValidationJobStore store;

    [TestInitialize]
    public void Initialize()
    {
        store = new ValidationJobStore();
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

        var validator1 = new Mock<IValidator>();
        validator1.SetupGet(v => v.Name).Returns("v1");
        var validator2 = new Mock<IValidator>();
        validator2.SetupGet(v => v.Name).Returns("v2");

        var validators = new List<IValidator> { validator1.Object, validator2.Object };
        store.StartJob(job.Id, validators, mandateId);
        var updated = store.GetJob(job.Id);

        Assert.IsNotNull(updated);
        Assert.AreEqual(Status.Processing, updated.Status);
        Assert.AreEqual(mandateId, updated.MandateId);
        Assert.HasCount(2, updated.ValidatorResults);
        Assert.IsTrue(updated.ValidatorResults.ContainsKey("v1"));
        Assert.IsTrue(updated.ValidatorResults.ContainsKey("v2"));

        // Check that validators are in the queue
        var queue = store.ValidationQueue;
        var readValidators = new List<IValidator>();
        while (queue.TryRead(out var v))
            readValidators.Add(v);

        Assert.HasCount(2, readValidators);
        Assert.Contains(validator1.Object, readValidators);
        Assert.Contains(validator2.Object, readValidators);
    }

    [TestMethod]
    public void StartJobThrowsIfNoValidators()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "a", "b");
        Assert.ThrowsExactly<ArgumentException>(() => store.StartJob(job.Id, new List<IValidator>(), 0));
    }

    [TestMethod]
    public void StartJobThrowsIfJobNotFound()
    {
        var validator = new Mock<IValidator>();
        validator.SetupGet(v => v.Name).Returns("v1");
        Assert.ThrowsExactly<ArgumentException>(() => store.StartJob(Guid.NewGuid(), new List<IValidator> { validator.Object }, 0));
    }

    [TestMethod]
    public void StartJobThrowsIfStatusNotReady()
    {
        var job = store.CreateJob();
        var validator = new Mock<IValidator>();
        validator.SetupGet(v => v.Name).Returns("v1");
        Assert.ThrowsExactly<InvalidOperationException>(() => store.StartJob(job.Id, new List<IValidator> { validator.Object }, 0));
    }

    [TestMethod]
    public void AddValidatorResult()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "a", "b");

        var validator = new Mock<IValidator>();
        validator.SetupGet(v => v.Name).Returns("v1");
        var validators = new List<IValidator> { validator.Object };
        store.StartJob(job.Id, validators, 0);

        var result = new ValidatorResult(ValidatorResultStatus.Completed, "some message");
        store.AddValidatorResult(validator.Object, result);
        var updated = store.GetJob(job.Id);

        Assert.IsNotNull(updated);
        Assert.AreEqual(Status.Completed, updated.Status);
        Assert.AreEqual(result, updated.ValidatorResults["v1"]);
    }

    [TestMethod]
    public void AddValidatorResultThrowsForSameValidatorTwice()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "a", "b");

        var validator = new Mock<IValidator>();
        validator.SetupGet(v => v.Name).Returns("v1");
        var validators = new List<IValidator> { validator.Object };
        store.StartJob(job.Id, validators, 0);

        // Complete the job
        var result = new ValidatorResult(ValidatorResultStatus.Completed, "some message");
        store.AddValidatorResult(validator.Object, result);

        // Try to add again, should throw
        Assert.ThrowsExactly<ArgumentException>(() => store.AddValidatorResult(validator.Object, result));
    }

    [TestMethod]
    public void AddValidatorResultThrowsForUnregisteredValidator()
    {
        var job = store.CreateJob();
        store.AddFileToJob(job.Id, "a", "b");

        var registeredValidator = new Mock<IValidator>();
        registeredValidator.SetupGet(v => v.Name).Returns("registeredValidator");
        var validators = new List<IValidator> { registeredValidator.Object };
        store.StartJob(job.Id, validators, 0);

        var unregisteredValidator = new Mock<IValidator>();
        unregisteredValidator.SetupGet(v => v.Name).Returns("unregisteredValidator");

        var result = new ValidatorResult(ValidatorResultStatus.Completed, "some message");

        // Try to add result for unregistered validator
        Assert.ThrowsExactly<ArgumentException>(() => store.AddValidatorResult(unregisteredValidator.Object, result));
    }
}
