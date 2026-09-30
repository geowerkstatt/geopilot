using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Geopilot.Api.Test.Authorization;

/// <summary>
/// Not parallelized: one of these empties the users of the shared test database to check how the first user is
/// registered. Emptying a shared table only holds while nothing else is signing in, and the tests that drive a
/// host over HTTP do exactly that. Rolling the transaction back afterwards does not help, because the other
/// connection commits in the meantime.
/// </summary>
[TestClass]
[DoNotParallelize]
public class GeopilotUserResolverTest
{
    private Mock<ILogger<GeopilotUserResolver>> loggerMock;
    private Mock<IGeopilotUserInfoService> userInfoServiceMock;
    private Mock<IHttpContextAccessor> httpContextAccessorMock;
    private Context context;
    private GeopilotUserResolver resolver;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<GeopilotUserResolver>>();
        userInfoServiceMock = new Mock<IGeopilotUserInfoService>();
        httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        context = AssemblyInitialize.DbFixture.GetTestContext();

        resolver = new GeopilotUserResolver(
            context,
            userInfoServiceMock.Object,
            httpContextAccessorMock.Object,
            loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        loggerMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    public async Task ResolveCreatesAndThenUpdatesTheUser()
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
        var user = await resolver.ResolveAsync();

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
        user = await resolver.ResolveAsync();

        // Assert
        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("PERFECTSTONE", user.FullName);
        Assert.AreEqual("DIRERUN@example.com", user.Email);
        Assert.IsFalse(user.IsAdmin);
    }

    [TestMethod]
    public async Task ResolveKeepsTheFirstUserNonAdmin()
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

        // Clear users with all relations in database. The execution protocol references the user as well, and
        // its rows outlive the job they belong to, so a test that started one leaves the users undeletable.
        context.Assets.RemoveRange(context.Assets);
        context.Deliveries.RemoveRange(context.Deliveries);
        context.PipelineRuns.RemoveRange(context.PipelineRuns);
        context.Users.RemoveRange(context.Users);
        context.SaveChanges();

        // Act
        var user = await resolver.ResolveAsync();

        // Assert
        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("STORMSLAW", user.FullName);
        Assert.AreEqual("MAIN@example.com", user.Email);
        Assert.IsFalse(user.IsAdmin);
    }

    [TestMethod]
    public async Task ResolveWithoutAnHttpContextReturnsNull()
    {
        var user = await resolver.ResolveAsync();

        Assert.IsNull(user);
    }

    [TestMethod]
    public async Task ResolveWithoutUserInfoReturnsNull()
    {
        var userCountBefore = context.Users.Count();
        SetupHttpContextWithToken("mock-token");
        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("mock-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserInfoResponse?)null);

        var user = await resolver.ResolveAsync();

        Assert.IsNull(user);
        Assert.AreEqual(userCountBefore, context.Users.Count());
    }

    [TestMethod]
    public async Task ResolveWithoutATokenReturnsNull()
    {
        // Arrange
        SetupHttpContextWithoutToken();

        // Act
        var user = await resolver.ResolveAsync();

        // Assert
        Assert.IsNull(user);
    }

    [TestMethod]
    public async Task ResolveWithCookieTokenUsesCookie()
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

        var user = await resolver.ResolveAsync();

        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        userInfoServiceMock.Verify(x => x.GetUserInfoAsync("cookie-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ResolveCookieWinsOverHeader()
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

        var user = await resolver.ResolveAsync();

        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        userInfoServiceMock.Verify(x => x.GetUserInfoAsync("cookie-token", It.IsAny<CancellationToken>()), Times.Once);
        userInfoServiceMock.Verify(x => x.GetUserInfoAsync("header-token", It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ResolveNeverTurnsARegisteredMachineClientIntoAUser()
    {
        var subject = Guid.NewGuid().ToString();
        context.MachineClients.Add(new MachineClient { AuthIdentifier = subject, Name = "SILENTHARBOR" });
        context.SaveChanges();

        // The identity provider would happily describe the token as a person; some do for service accounts.
        SetupHttpContextWithToken("mock-token", subject);
        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("mock-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserInfoResponse { Sub = subject, Email = "robot@example.com", Name = "Robot" });

        var user = await resolver.ResolveAsync();

        Assert.IsNull(user, "A registered client must not become a user, or its credentials would open the web interface.");
        Assert.IsFalse(context.Users.Any(u => u.AuthIdentifier == subject), "No user row may have been created for the client.");
        userInfoServiceMock.Verify(x => x.GetUserInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never, "The decision must not depend on what the identity provider answers.");
    }

    [TestMethod]
    public async Task PrefetchSkipsARegisteredMachineClient()
    {
        var subject = Guid.NewGuid().ToString();
        context.MachineClients.Add(new MachineClient { AuthIdentifier = subject, Name = "QUIETLANTERN" });
        context.SaveChanges();

        var userInfo = await resolver.PrefetchUserInfoAsync(subject, "mock-token", CancellationToken.None);

        Assert.IsNull(userInfo);
        userInfoServiceMock.Verify(
            x => x.GetUserInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "A machine names no person, so there is nothing to ask the identity provider, and asking anyway logs a failed request for every call of the machine.");
    }

    [TestMethod]
    public async Task PrefetchRequestsTheUserInfoOfAPerson()
    {
        var expected = new UserInfoResponse { Sub = Guid.NewGuid().ToString(), Email = "SILVERCREEK@example.com", Name = "SILVERCREEK" };
        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("mock-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var userInfo = await resolver.PrefetchUserInfoAsync(expected.Sub, "mock-token", CancellationToken.None);

        Assert.AreSame(expected, userInfo);
        userInfoServiceMock.Verify(x => x.GetUserInfoAsync("mock-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task PrefetchWithoutASubjectRequestsTheUserInfo()
    {
        // An introspection response that names neither sub nor client_id leaves the opaque handler with the
        // user info as the only source of a subject, so nothing may be skipped here.
        var expected = new UserInfoResponse { Sub = Guid.NewGuid().ToString(), Email = "MAPLEHOLLOW@example.com", Name = "MAPLEHOLLOW" };
        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("mock-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var userInfo = await resolver.PrefetchUserInfoAsync(null, "mock-token", CancellationToken.None);

        Assert.AreSame(expected, userInfo);
    }

    [TestMethod]
    public async Task PrefetchLetsAnUnavailableIdentityProviderThrough()
    {
        userInfoServiceMock.Setup(x => x.GetUserInfoAsync("mock-token", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IdentityProviderUnavailableException("User info request failed."));

        await Assert.ThrowsExactlyAsync<IdentityProviderUnavailableException>(
            () => resolver.PrefetchUserInfoAsync(Guid.NewGuid().ToString(), "mock-token", CancellationToken.None),
            "The authentication handlers turn this into a 503; swallowing it here would leave them a 403.");
    }

    private void SetupHttpContextWithToken(string token, string? subject = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {token}";
        if (subject is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, subject)], "Test"));
        }

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
