using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("mock-token"))
            .ReturnsAsync(userInfo);

        var authHandlerContext = new AuthorizationHandlerContext(
            Enumerable.Empty<IAuthorizationRequirement>(),
            new ClaimsPrincipal(),
            null);

        // Act - Create user
        var user = await geopilotUserHandler.UpdateOrCreateUser(authHandlerContext);

        // Assert
        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("BROOMNEIGHBOR", user.FullName);
        Assert.AreEqual("ONYXSHADOW@example.com", user.Email);
        Assert.AreEqual(false, user.IsAdmin);

        // Arrange - Update user
        var updatedUserInfo = new UserInfoResponse
        {
            Sub = authIdentifier,
            Email = "DIRERUN@example.com",
            Name = "PERFECTSTONE",
        };

        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("mock-token"))
            .ReturnsAsync(updatedUserInfo);

        // Act - Update user
        user = await geopilotUserHandler.UpdateOrCreateUser(authHandlerContext);

        // Assert
        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("PERFECTSTONE", user.FullName);
        Assert.AreEqual("DIRERUN@example.com", user.Email);
        Assert.AreEqual(false, user.IsAdmin);
    }

    [TestMethod]
    public async Task UpdateOrCreateUserElevatesFirstUserToAdmin()
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
        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("mock-token"))
            .ReturnsAsync(userInfo);

        var authHandlerContext = new AuthorizationHandlerContext(
            Enumerable.Empty<IAuthorizationRequirement>(),
            new ClaimsPrincipal(),
            null);

        // Clear users with all relations in database
        context.Assets.RemoveRange(context.Assets);
        context.Deliveries.RemoveRange(context.Deliveries);
        context.Users.RemoveRange(context.Users);
        context.SaveChanges();

        // Act
        var user = await geopilotUserHandler.UpdateOrCreateUser(authHandlerContext);

        // Assert
        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("STORMSLAW", user.FullName);
        Assert.AreEqual("MAIN@example.com", user.Email);
        Assert.AreEqual(true, user.IsAdmin);
    }

    [TestMethod]
    public async Task UpdateOrCreateUserWithMissingClaimsDoesNothing()
    {
        var principalWithMissingClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
        }));

        var authHandlerContext = new AuthorizationHandlerContext(Enumerable.Empty<IAuthorizationRequirement>(), principalWithMissingClaims, null);

        var user = await geopilotUserHandler.UpdateOrCreateUser(authHandlerContext);
        Assert.IsNull(user);
    }

    [TestMethod]
    public async Task UpdateOrCreateUserWithMissingTokenReturnsNull()
    {
        // Arrange
        SetupHttpContextWithoutToken();

        var authHandlerContext = new AuthorizationHandlerContext(
            Enumerable.Empty<IAuthorizationRequirement>(),
            new ClaimsPrincipal(),
            null);

        // Act
        var user = await geopilotUserHandler.UpdateOrCreateUser(authHandlerContext);

        // Assert
        Assert.IsNull(user);
    }

    private void SetupHttpContextWithToken(string token)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {token}";
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }

    private void SetupHttpContextWithoutToken()
    {
        var httpContext = new DefaultHttpContext();
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }
}
