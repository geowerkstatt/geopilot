using GeoCop.Api.Models;
using GeoCop.Api.Test;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace GeoCop.Api.Controllers;

[TestClass]
public class UserControllerTest
{
    private Mock<HttpContext> httpContextMock;
    private Context context;
    private UserController userController;

    [TestInitialize]
    public void Setup()
    {
        httpContextMock = new Mock<HttpContext>();
        context = Initialize.DbFixture.GetTestContext();
        userController = new UserController(context);
        userController.ControllerContext.HttpContext = httpContextMock.Object;
    }

    [TestCleanup]
    public void Cleanup()
    {
        httpContextMock.VerifyAll();
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
            FullName = "old name",
            Email = email,
            IsAdmin = true,
        };
        context.Users.Add(dbUser);
        context.SaveChanges();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ContextExtensions.UserIdClaim, authIdentifier),
            new Claim(ContextExtensions.NameClaim, fullName),
            new Claim(ContextExtensions.EmailClaim, email),
        }));
        httpContextMock.SetupGet(c => c.User).Returns(principal);

        var userResult = await userController.GetAsync();

        context.ChangeTracker.Clear();

        Assert.IsNotNull(userResult);
        Assert.AreEqual(authIdentifier, userResult.AuthIdentifier);
        Assert.AreEqual(fullName, userResult.FullName);
        Assert.AreEqual(email, userResult.Email);
        Assert.AreEqual(true, userResult.IsAdmin);
    }

    [TestMethod]
    public async Task GetUserAsyncNotFound()
    {
        var authIdentifier = Guid.Empty.ToString();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ContextExtensions.UserIdClaim, authIdentifier),
            new Claim(ContextExtensions.NameClaim, ""),
            new Claim(ContextExtensions.EmailClaim, ""),
        }));
        httpContextMock.SetupGet(c => c.User).Returns(principal);

        var userResult = await userController.GetAsync();

        context.ChangeTracker.Clear();

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
        httpContextMock.SetupGet(c => c.User).Returns(principal);

        var userResult = await userController.GetAsync();

        context.ChangeTracker.Clear();

        Assert.IsNull(userResult);
    }
}
