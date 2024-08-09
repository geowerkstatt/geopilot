using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
            ClientId = Guid.NewGuid().ToString(),
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
        const string fullName = "Full Name";
        const string email = "some@email.com";

        var dbUser = new User
        {
            AuthIdentifier = authIdentifier,
            FullName = fullName,
            Email = email,
            IsAdmin = true,
        };
        context.Users.Add(dbUser);
        context.SaveChanges();

        var httpContextMock = userController.SetupTestUser(dbUser);

        dbUser.FullName = "This value should be replaced by name claim";
        context.SaveChanges();

        var userResult = await userController.GetSelfAsync();

        Assert.IsNotNull(userResult);
        Assert.AreEqual(authIdentifier, userResult.AuthIdentifier);
        Assert.AreEqual(fullName, userResult.FullName);
        Assert.AreEqual(email, userResult.Email);
        Assert.AreEqual(true, userResult.IsAdmin);
        httpContextMock.VerifyAll();
    }

    [TestMethod]
    public async Task GetCurrentUserAsyncNotFound()
    {
        var user = new User
        {
            AuthIdentifier = Guid.Empty.ToString(),
        };

        var httpContextMock = userController.SetupTestUser(user);

        var userResult = await userController.GetSelfAsync();

        Assert.IsNull(userResult);
        httpContextMock.VerifyAll();
    }

    [TestMethod]
    public async Task GetCurrentUserAsyncMissingClaims()
    {
        var authIdentifier = Guid.NewGuid().ToString();

        var dbUser = new User
        {
            AuthIdentifier = authIdentifier,
        };
        context.Users.Add(dbUser);
        context.SaveChanges();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, authIdentifier),
        }));

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.SetupGet(c => c.User).Returns(principal);
        userController.ControllerContext.HttpContext = httpContextMock.Object;

        var userResult = await userController.GetSelfAsync();

        Assert.IsNull(userResult);
        httpContextMock.VerifyAll();
    }

    [TestMethod]
    public async Task GetUserById()
    {
        var testUser = new User
        {
            AuthIdentifier = Guid.NewGuid().ToString(),
            FullName = "AGILITY MOSES",
            Email = "agility@moses.com",
            IsAdmin = true,
        };
        context.Users.Add(testUser);
        context.SaveChanges();

        var userResult = await userController.GetById(testUser.Id);
        ActionResultAssert.IsOk(userResult);
        var user = (userResult as OkObjectResult)?.Value as User;
        Assert.IsNotNull(user);
        Assert.AreEqual(testUser.AuthIdentifier, user.AuthIdentifier);
        Assert.AreEqual(testUser.FullName, user.FullName);
        Assert.AreEqual(testUser.Email, user.Email);
        Assert.AreEqual(testUser.IsAdmin, user.IsAdmin);
    }

    [TestMethod]
    public async Task GetUserByIdNotFound()
    {
        var userResult = await userController.GetById(0);
        ActionResultAssert.IsNotFound(userResult);
        Assert.IsNotNull(userResult);
    }

    [TestMethod]
    public void GetUsers()
    {
        var testUser = new User
        {
            AuthIdentifier = Guid.NewGuid().ToString(),
            FullName = "Test User",
            Email = "test@user.com",
            IsAdmin = true,
        };
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
        Assert.AreEqual(browserAuthOptions.ClientId, authOptions.ClientId);
    }

    [TestMethod]
    public async Task EditUser()
    {
        var testUser = new User
        {
            AuthIdentifier = Guid.NewGuid().ToString(),
            FullName = "FLEA XI",
            Email = "flea@xi.com",
            IsAdmin = false,
        };
        context.Users.Add(testUser);
        context.SaveChanges();

        var userResult = await userController.GetById(testUser.Id) as OkObjectResult;
        var user = userResult?.Value as User;
        Assert.IsNotNull(user);
        user.FullName = "FLEA XI Updated";
        user.IsAdmin = true;
        user.Organisations = new List<Organisation> { new () { Id = 1 }, new () { Id = 2 } };

        var result = await userController.Edit(user);
        ActionResultAssert.IsOk(result);
        var resultValue = (result as OkObjectResult)?.Value as User;
        Assert.IsNotNull(resultValue);
        Assert.AreEqual("FLEA XI", resultValue.FullName);
        Assert.IsTrue(resultValue.IsAdmin);
        Assert.AreEqual(2, resultValue.Organisations.Count);
        for (var i = 0; i < 2; i++)
        {
            Assert.AreEqual(testUser.Organisations[i].Id, resultValue.Organisations[i].Id);
        }

        testUser.Organisations = new List<Organisation> { new () { Id = 2 }, new () { Id = 3 } };
        result = await userController.Edit(testUser);
        ActionResultAssert.IsOk(result);
        resultValue = (result as OkObjectResult)?.Value as User;
        Assert.IsNotNull(resultValue);
        Assert.AreEqual(2, resultValue.Organisations.Count);
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
