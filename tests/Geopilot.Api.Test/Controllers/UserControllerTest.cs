using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Api.Controllers;

[TestClass]
public class UserControllerTest
{
    private Context context;
    private Mock<IOptions<BrowserAuthOptions>> authOptionsMock;
    private BrowserAuthOptions browserAuthOptions;
    private UserController userController;

    [TestInitialize]
    public void Initialize()
    {
        var loggerMock = new Mock<ILogger<UserController>>();
        context = AssemblyInitialize.DbFixture.GetTestContext();
        authOptionsMock = new Mock<IOptions<BrowserAuthOptions>>();
        browserAuthOptions = new BrowserAuthOptions
        {
            Authority = "https://localhost/some-authority",
            ClientAudience = Guid.NewGuid().ToString(),
            FullScope = "profile email openid geopilot.api",
        };
        authOptionsMock.SetupGet(o => o.Value).Returns(browserAuthOptions);

        userController = new UserController(loggerMock.Object, context, authOptionsMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        authOptionsMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    public async Task GetCurrentUserAsync()
    {
        var authIdentifier = Guid.NewGuid().ToString();
        var dbUser = CreateUser(authIdentifier, "Full Name", "some@email.com", isAdmin: true);
        context.Users.Add(dbUser);
        context.SaveChanges();

        var httpContextMock = userController.SetupTestUser(dbUser);

        var userResult = await userController.GetSelfAsync();
        var user = ActionResultAssert.IsOkObjectResult<User>(userResult);
        Assert.IsNotNull(userResult);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("Full Name", user.FullName);
        Assert.AreEqual("some@email.com", user.Email);
        Assert.IsTrue(user.IsAdmin);
        httpContextMock.VerifyAll();
    }

    [TestMethod]
    public async Task GetCurrentUserAsyncForUnknownUserThrowsException()
    {
        var user = new User
        {
            AuthIdentifier = Guid.Empty.ToString(),
        };

        var httpContextMock = userController.SetupTestUser(user);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await userController.GetSelfAsync());
        httpContextMock.VerifyAll();
    }

    [TestMethod]
    public async Task GetUserById()
    {
        var testUser = CreateUser(Guid.NewGuid().ToString(), "AGILITY MOSES", "agility@moses.com", isAdmin: true);
        context.Users.Add(testUser);
        context.SaveChanges();

        var userResult = await userController.GetById(testUser.Id);
        var user = ActionResultAssert.IsOkObjectResult<User>(userResult);
        Assert.AreEqual(testUser.AuthIdentifier, user.AuthIdentifier);
        Assert.AreEqual(testUser.FullName, user.FullName);
        Assert.AreEqual(testUser.Email, user.Email);
        Assert.AreEqual(testUser.IsAdmin, user.IsAdmin);
    }

    [TestMethod]
    public async Task GetUserByIdNotFound()
    {
        var userResult = await userController.GetById(int.MaxValue);
        ActionResultAssert.IsNotFound(userResult);
        Assert.IsNotNull(userResult);
    }

    [TestMethod]
    public void GetUsers()
    {
        var testUser = CreateUser(Guid.NewGuid().ToString(), "Test User", "test@user.com", isAdmin: true);
        context.Users.Add(testUser);
        context.SaveChanges();

        var users = userController.Get();

        Assert.IsNotNull(users);
        var lastUser = users.Last();
        Assert.AreEqual(testUser.AuthIdentifier, lastUser.AuthIdentifier);
        Assert.AreEqual(testUser.FullName, lastUser.FullName);
        Assert.AreEqual(testUser.Email, lastUser.Email);
        Assert.AreEqual(testUser.IsAdmin, lastUser.IsAdmin);
    }

    [TestMethod]
    public void GetAuthOptions()
    {
        var authOptions = userController.GetAuthOptions();

        Assert.IsNotNull(authOptions);
        Assert.AreEqual(browserAuthOptions.Authority, authOptions.Authority);
        Assert.AreEqual(browserAuthOptions.ClientAudience, authOptions.ClientAudience);
    }

    [TestMethod]
    public async Task EditUser()
    {
        var testUser = CreateUser(Guid.NewGuid().ToString(), "FLEA XI", "flea@xi.com", isAdmin: false, state: UserState.Inactive);
        context.Users.Add(testUser);
        context.SaveChanges();

        var userResult = await userController.GetById(testUser.Id) as OkObjectResult;
        var user = userResult?.Value as User;
        Assert.IsNotNull(user);
        user.FullName = "FLEA XI Updated";
        user.IsAdmin = true;
        user.State = UserState.Active;
        user.Organisations = new List<Organisation> { new() { Id = 1 }, new() { Id = 2 } };

        var result = await userController.Edit(user);
        var resultValue = ActionResultAssert.IsOkObjectResult<User>(result);
        Assert.IsNotNull(resultValue);
        Assert.AreEqual("FLEA XI", resultValue.FullName);
        Assert.IsTrue(resultValue.IsAdmin);
        Assert.AreEqual(UserState.Active, resultValue.State);
        Assert.HasCount(2, resultValue.Organisations);
        for (var i = 0; i < 2; i++)
        {
            Assert.AreEqual(testUser.Organisations[i].Id, resultValue.Organisations[i].Id);
        }

        testUser.Organisations = new List<Organisation> { new() { Id = 2 }, new() { Id = 3 } };
        result = await userController.Edit(testUser);
        resultValue = ActionResultAssert.IsOkObjectResult<User>(result);
        Assert.IsNotNull(resultValue);
        Assert.HasCount(2, resultValue.Organisations);
        for (var i = 0; i < 2; i++)
        {
            Assert.AreEqual(testUser.Organisations[i].Id, resultValue.Organisations[i].Id);
        }
    }

    [TestMethod]
    public async Task EditUserNotFound()
    {
        var result = await userController.Edit(new User { Id = 0 });
        ActionResultAssert.IsNotFound(result);
    }
}
