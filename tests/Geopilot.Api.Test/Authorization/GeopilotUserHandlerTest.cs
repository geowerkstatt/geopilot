using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Moq;

namespace Geopilot.Api.Test.Authorization;

[TestClass]
public class GeopilotUserHandlerTest
{
    private Mock<ILogger<GeopilotUserHandler>> loggerMock;
    private Mock<IGeopilotUserInfoService> userInfoServiceMock;
    private Mock<IHttpContextAccessor> httpContextAccessorMock;
    private Context context;
    private GeopilotUserHandler geopilotUserHandler;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<GeopilotUserHandler>>();
        userInfoServiceMock = new Mock<IGeopilotUserInfoService>();
        httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        context = AssemblyInitialize.DbFixture.GetTestContext();

        geopilotUserHandler = new GeopilotUserHandler(
            loggerMock.Object,
            context,
            userInfoServiceMock.Object,
            httpContextAccessorMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        loggerMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    public async Task UpdateOrCreateUser()
    {
        // Arrange
        var authIdentifier = Guid.NewGuid().ToString();
        var userInfo = new UserInfoResponse
        {
            Sub = authIdentifier,
            Email = "ONYXSHADOW@example.com",
            Name = "BROOMNEIGHBOR",
        };

        SetupHttpContextWithToken("mock-token");
        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("mock-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(userInfo);

        // Act - Create user
        var user = await geopilotUserHandler.UpdateOrCreateUser();

        // Assert
        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("BROOMNEIGHBOR", user.FullName);
        Assert.AreEqual("ONYXSHADOW@example.com", user.Email);
        Assert.IsFalse(user.IsAdmin);

        // Arrange - Update user
        var updatedUserInfo = new UserInfoResponse
        {
            Sub = authIdentifier,
            Email = "DIRERUN@example.com",
            Name = "PERFECTSTONE",
        };

        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("mock-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedUserInfo);

        // Act - Update user
        user = await geopilotUserHandler.UpdateOrCreateUser();

        // Assert
        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("PERFECTSTONE", user.FullName);
        Assert.AreEqual("DIRERUN@example.com", user.Email);
        Assert.IsFalse(user.IsAdmin);
    }

    [TestMethod]
    public async Task FirstUserRemainsNonAdmin()
    {
        // Arrange
        var authIdentifier = Guid.NewGuid().ToString();
        var userInfo = new UserInfoResponse
        {
            Sub = authIdentifier,
            Email = "MAIN@example.com",
            Name = "STORMSLAW",
        };

        SetupHttpContextWithToken("mock-token");
        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("mock-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(userInfo);

        // Clear users with all relations in database
        context.Assets.RemoveRange(context.Assets);
        context.Deliveries.RemoveRange(context.Deliveries);
        context.Users.RemoveRange(context.Users);
        context.SaveChanges();

        // Act
        var user = await geopilotUserHandler.UpdateOrCreateUser();

        // Assert
        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("STORMSLAW", user.FullName);
        Assert.AreEqual("MAIN@example.com", user.Email);
        Assert.IsFalse(user.IsAdmin);
    }

    [TestMethod]
    public async Task UpdateOrCreateUserWithoutHttpContextDoesNothing()
    {
        var user = await geopilotUserHandler.UpdateOrCreateUser();
        Assert.IsNull(user);
    }

    [TestMethod]
    public async Task UpdateOrCreateUserWithoutUserInfoReturnsNull()
    {
        var userCountBefore = context.Users.Count();
        SetupHttpContextWithToken("mock-token");
        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("mock-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserInfoResponse?)null);

        var user = await geopilotUserHandler.UpdateOrCreateUser();

        Assert.IsNull(user);
        Assert.AreEqual(userCountBefore, context.Users.Count());
    }

    [TestMethod]
    public async Task UpdateOrCreateUserWithMissingTokenReturnsNull()
    {
        // Arrange
        SetupHttpContextWithoutToken();

        // Act
        var user = await geopilotUserHandler.UpdateOrCreateUser();

        // Assert
        Assert.IsNull(user);
    }

    [TestMethod]
    public async Task UpdateOrCreateUserWithCookieTokenUsesCookie()
    {
        var authIdentifier = Guid.NewGuid().ToString();
        var userInfo = new UserInfoResponse
        {
            Sub = authIdentifier,
            Email = "cookie-user@example.com",
            Name = "Cookie User",
        };

        SetupHttpContextWithCookie("cookie-token");
        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("cookie-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(userInfo);

        var user = await geopilotUserHandler.UpdateOrCreateUser();

        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        userInfoServiceMock.Verify(x => x.GetUserInfoAsync("cookie-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UpdateOrCreateUserCookieWinsOverHeader()
    {
        var authIdentifier = Guid.NewGuid().ToString();
        var userInfo = new UserInfoResponse
        {
            Sub = authIdentifier,
            Email = "cookie-user@example.com",
            Name = "Cookie User",
        };

        SetupHttpContextWithCookieAndHeader("cookie-token", "header-token");
        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("cookie-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(userInfo);

        var user = await geopilotUserHandler.UpdateOrCreateUser();

        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        userInfoServiceMock.Verify(x => x.GetUserInfoAsync("cookie-token", It.IsAny<CancellationToken>()), Times.Once);
        userInfoServiceMock.Verify(x => x.GetUserInfoAsync("header-token", It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupHttpContextWithToken(string token)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {token}";
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }

    private void SetupHttpContextWithCookie(string token)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Append(HeaderNames.Cookie, $"{AuthDefaults.AuthCookieName}={token}");
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }

    private void SetupHttpContextWithCookieAndHeader(string cookieToken, string headerToken)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Append(HeaderNames.Cookie, $"{AuthDefaults.AuthCookieName}={cookieToken}");
        httpContext.Request.Headers.Append(HeaderNames.Authorization, $"Bearer {headerToken}");
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }

    private void SetupHttpContextWithoutToken()
    {
        var httpContext = new DefaultHttpContext();
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }
}
