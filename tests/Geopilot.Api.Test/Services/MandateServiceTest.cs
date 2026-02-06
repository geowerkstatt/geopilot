using Geopilot.Api.Models;
using Geopilot.Api.Services;
using Geopilot.Api.Validation;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class MandateServiceTest
{
    private Context context;
    private MandateService mandateService;
    private Mock<IValidationJobStore> validationJobStoreMock;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        validationJobStoreMock = new Mock<IValidationJobStore>(MockBehavior.Strict);

        mandateService = new MandateService(context, validationJobStoreMock.Object);
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

        var result = await mandateService.GetMandateAsUser(publicMandate.Id, user);

        Assert.IsNotNull(result);
        Assert.AreEqual(publicMandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsPublicMandateForUnauthenticatedUser()
    {
        var publicMandate = context.Mandates.Add(new Mandate { Name = "Public Mandate", IsPublic = true }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateAsUser(publicMandate.Id, null);

        Assert.IsNotNull(result);
        Assert.AreEqual(publicMandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsNullForNonPublicMandateWhenUnauthenticated()
    {
        var privateMandate = context.Mandates.Add(new Mandate { Name = "Private Mandate", IsPublic = false }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateAsUser(privateMandate.Id, null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsMandateForAuthorizedUser()
    {
        var (user, mandate) = context.AddMandateWithUserOrganisation();

        var result = await mandateService.GetMandateAsUser(mandate.Id, user);

        Assert.IsNotNull(result);
        Assert.AreEqual(mandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsNullForUnauthorizedUser()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        var mandate = context.Mandates.Add(new Mandate { Name = "Restricted Mandate", IsPublic = false }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateAsUser(mandate.Id, user);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsNullForNonExistentMandate()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateAsUser(int.MaxValue, user);

        Assert.IsNull(result);
    }
}
