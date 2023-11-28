using GeoCop.Api.Models;
using GeoCop.Api.Test;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace GeoCop.Api.Controllers;

[TestClass]
public class UserControllerTest
{
    private Context context;
    private UserController userController;

    [TestInitialize]
    public void Setup()
    {
        context = Initialize.DbFixture.GetTestContext();
        userController = new UserController(context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        context.Dispose();
    }

    [TestMethod]
    public async Task GetUserAsync()
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

        userController.SetupTestUser(dbUser);

        dbUser.FullName = "This value should be replaced by name claim";
        context.SaveChanges();

        var userResult = await userController.GetAsync();

        Assert.IsNotNull(userResult);
        Assert.AreEqual(authIdentifier, userResult.AuthIdentifier);
        Assert.AreEqual(fullName, userResult.FullName);
        Assert.AreEqual(email, userResult.Email);
        Assert.AreEqual(true, userResult.IsAdmin);
    }

    [TestMethod]
    public async Task GetUserAsyncNotFound()
    {
        var user = new User
        {
            AuthIdentifier = Guid.Empty.ToString(),
        };

        userController.SetupTestUser(user);

        var userResult = await userController.GetAsync();

        Assert.IsNull(userResult);
    }

    [TestMethod]
    public async Task GetUserAsyncMissingClaims()
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
            new Claim(ContextExtensions.UserIdClaim, authIdentifier),
        }));

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.SetupGet(c => c.User).Returns(principal);
        userController.ControllerContext.HttpContext = httpContextMock.Object;

        var userResult = await userController.GetAsync();

        Assert.IsNull(userResult);
        httpContextMock.VerifyAll();
    }
}
