using Geopilot.Api.Models;
using Geopilot.Api.Services;
using Geopilot.Api.Validation;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class MandateServiceTest
{
    private Context context;
    private MandateService mandateService;
    private Mock<IValidationJobStore> validationJobStoreMock;
    private User editUser;
    private User adminUser;
    private Mandate unrestrictedMandate;
    private Mandate noDeliveryMandate;
    private Mandate xtfMandate;
    private Mandate publicCsvMandate;
    private Mandate noOrganisationsMandate;
    private Mandate noPermissionMandate;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        validationJobStoreMock = new Mock<IValidationJobStore>(MockBehavior.Strict);

        mandateService = new MandateService(context, validationJobStoreMock.Object);

        unrestrictedMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = nameof(unrestrictedMandate), AllowDelivery = true };
        noDeliveryMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = nameof(noDeliveryMandate), AllowDelivery = false };
        xtfMandate = new Mandate { FileTypes = new string[] { ".xtf" }, Name = nameof(xtfMandate), AllowDelivery = true };
        publicCsvMandate = new Mandate { FileTypes = new string[] { ".csv" }, Name = nameof(publicCsvMandate), IsPublic = true, AllowDelivery = true };
        noOrganisationsMandate = new Mandate { FileTypes = new string[] { ".itf" }, Name = nameof(noOrganisationsMandate), AllowDelivery = true };
        noPermissionMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = nameof(noPermissionMandate), AllowDelivery = true };

        context.Mandates.Add(unrestrictedMandate);
        context.Mandates.Add(noDeliveryMandate);
        context.Mandates.Add(xtfMandate);
        context.Mandates.Add(publicCsvMandate);
        context.Mandates.Add(noOrganisationsMandate);
        context.Mandates.Add(noPermissionMandate);

        editUser = CreateUser("ms-123", "Edit User", "example@example.org");
        context.Users.Add(editUser);

        adminUser = CreateUser("ms-1234", "Admin User", "admin.example@example.org", isAdmin: true);
        context.Users.Add(adminUser);

        var organisation = new Organisation { Name = "GAMMAHUNT" };
        organisation.Mandates.Add(unrestrictedMandate);
        organisation.Mandates.Add(noDeliveryMandate);
        organisation.Mandates.Add(xtfMandate);
        organisation.Mandates.Add(publicCsvMandate);
        organisation.Users.Add(editUser);
        organisation.Users.Add(adminUser);

        var organisation2 = new Organisation { Name = "DELTALIGHT" };
        organisation2.Mandates.Add(noPermissionMandate);
        organisation2.Users.Add(adminUser);

        context.Add(organisation);
        context.Add(organisation2);
        context.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup()
    {
        validationJobStoreMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsPublicMandateForAuthenticatedUser()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        var publicMandate = context.Mandates.Add(new Mandate { Name = "Public Mandate", IsPublic = true }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForUser(publicMandate.Id, user);

        Assert.IsNotNull(result);
        Assert.AreEqual(publicMandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsPublicMandateForUnauthenticatedUser()
    {
        var publicMandate = context.Mandates.Add(new Mandate { Name = "Public Mandate", IsPublic = true }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForUser(publicMandate.Id, null);

        Assert.IsNotNull(result);
        Assert.AreEqual(publicMandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsNullForNonPublicMandateWhenUnauthenticated()
    {
        var privateMandate = context.Mandates.Add(new Mandate { Name = "Private Mandate", IsPublic = false }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForUser(privateMandate.Id, null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsMandateForAuthorizedUser()
    {
        var (user, mandate) = context.AddMandateWithUserOrganisation();

        var result = await mandateService.GetMandateForUser(mandate.Id, user);

        Assert.IsNotNull(result);
        Assert.AreEqual(mandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsNullForUnauthorizedUser()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        var mandate = context.Mandates.Add(new Mandate { Name = "Restricted Mandate", IsPublic = false }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForUser(mandate.Id, user);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsNullForNonExistentMandate()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForUser(int.MaxValue, user);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetMandatesAsNonAdminUser()
    {
        var result = await mandateService.GetMandatesAsync(editUser);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        ContainsMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, noPermissionMandate);
    }

    [TestMethod]
    public async Task GetMandatesAsAdminUser()
    {
        var result = await mandateService.GetMandatesAsync(adminUser);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        ContainsMandate(result, publicCsvMandate);
        ContainsMandate(result, noOrganisationsMandate);
        ContainsMandate(result, noPermissionMandate);
    }

    [TestMethod]
    public async Task GetMandatesWithJobIdAsNonAdmin()
    {
        var jobId = Guid.NewGuid();
        validationJobStoreMock
            .Setup(m => m.GetJob(jobId))
            .Returns(new ValidationJob(jobId, "Original.xtf", "tmp.xtf", null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now));

        var result = await mandateService.GetMandatesAsync(editUser, jobId);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        DoesNotContainMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, noPermissionMandate);
    }

    [TestMethod]
    public async Task GetMandatesWithJobIdAsAdmin()
    {
        var jobId = Guid.NewGuid();
        validationJobStoreMock
            .Setup(m => m.GetJob(jobId))
            .Returns(new ValidationJob(jobId, "Original.xtf", "tmp.xtf", null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now));

        var result = await mandateService.GetMandatesAsync(adminUser, jobId);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        ContainsMandate(result, noPermissionMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, publicCsvMandate);
    }

    [TestMethod]
    public async Task GetMandatesAsUnauthenticated()
    {
        var result = await mandateService.GetMandatesAsync(null);

        ContainsMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, xtfMandate);
        DoesNotContainMandate(result, unrestrictedMandate);
        DoesNotContainMandate(result, noDeliveryMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, noPermissionMandate);
    }

    [TestMethod]
    public async Task GetMandatesWithJobIdAsUnauthenticated()
    {
        var jobId = Guid.NewGuid();
        validationJobStoreMock
            .Setup(m => m.GetJob(jobId))
            .Returns(new ValidationJob(jobId, "Original.xtf", "tmp.xtf", null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now));

        var result = await mandateService.GetMandatesAsync(null, jobId);

        DoesNotContainMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, unrestrictedMandate);
        DoesNotContainMandate(result, noDeliveryMandate);
        DoesNotContainMandate(result, xtfMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, noPermissionMandate);
    }

    [TestMethod]
    public async Task GetMandatesWithJobIdIgnoresCase()
    {
        var jobId = Guid.NewGuid();
        validationJobStoreMock
            .Setup(m => m.GetJob(jobId))
            .Returns(new ValidationJob(jobId, "Original.XTF", "tmp.XTF", null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now));

        var result = await mandateService.GetMandatesAsync(editUser, jobId);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        DoesNotContainMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
    }

    [TestMethod]
    public async Task GetMandatesWithInvalidJobIdThrows()
    {
        var jobId = Guid.NewGuid();
        validationJobStoreMock
            .Setup(m => m.GetJob(jobId))
            .Returns((ValidationJob?)null);

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await mandateService.GetMandatesAsync(editUser, jobId));
    }

    [TestMethod]
    public async Task GetMandatesWithCloudFilesFiltersByExtension()
    {
        var jobId = Guid.NewGuid();
        var cloudFiles = ImmutableList.Create(new CloudFileInfo("data.xtf", "blobs/data.xtf", 1024));
        validationJobStoreMock
            .Setup(m => m.GetJob(jobId))
            .Returns(new ValidationJob(jobId, null, null, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now, Enums.UploadMethod.Cloud, cloudFiles));

        var result = await mandateService.GetMandatesAsync(editUser, jobId);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        DoesNotContainMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, noPermissionMandate);
    }

    private void ContainsMandate(IEnumerable<Mandate> mandates, Mandate mandate)
    {
        var found = mandates.FirstOrDefault(m => m.Id == mandate.Id);
        Assert.IsNotNull(found, $"mandate with id '{mandate.Id}' and name '{mandate.Name}' not found");
    }

    private void DoesNotContainMandate(IEnumerable<Mandate> mandates, Mandate mandate)
    {
        var found = mandates.FirstOrDefault(m => m.Id == mandate.Id);
        Assert.IsNull(found);
    }
}
