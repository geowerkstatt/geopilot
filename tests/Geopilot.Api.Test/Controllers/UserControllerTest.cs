using Castle.Core.Logging;
using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Api.Test;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
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
            RedirectUri = "/",
            PostLogoutRedirectUri = "/logout",
            NavigateToLoginRequestUrl = false,
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

        var httpContextMock = userController.SetupTestUser(dbUser);

        dbUser.FullName = "This value should be replaced by name claim";
        context.SaveChanges();

        var userResult = await userController.GetAsync();

        Assert.IsNotNull(userResult);
        Assert.AreEqual(authIdentifier, userResult.AuthIdentifier);
        Assert.AreEqual(fullName, userResult.FullName);
        Assert.AreEqual(email, userResult.Email);
        Assert.AreEqual(true, userResult.IsAdmin);
        httpContextMock.VerifyAll();
    }

    [TestMethod]
    public async Task GetUserAsyncNotFound()
    {
        var user = new User
        {
            AuthIdentifier = Guid.Empty.ToString(),
        };

        var httpContextMock = userController.SetupTestUser(user);

        var userResult = await userController.GetAsync();

        Assert.IsNull(userResult);
        httpContextMock.VerifyAll();
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

    [TestMethod]
    public void GetAuthOptions()
    {
        var authOptions = userController.GetAuthOptions();

        Assert.IsNotNull(authOptions);
        Assert.AreEqual(browserAuthOptions.Authority, authOptions.Authority);
        Assert.AreEqual(browserAuthOptions.ClientId, authOptions.ClientId);
        Assert.AreEqual(browserAuthOptions.RedirectUri, authOptions.RedirectUri);
        Assert.AreEqual(browserAuthOptions.PostLogoutRedirectUri, authOptions.PostLogoutRedirectUri);
        Assert.AreEqual(browserAuthOptions.NavigateToLoginRequestUrl, authOptions.NavigateToLoginRequestUrl);
    }
}
