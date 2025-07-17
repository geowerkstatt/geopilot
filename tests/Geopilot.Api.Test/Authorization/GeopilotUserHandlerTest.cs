using Geopilot.Api.Authorization;
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
    private Mock<GeopilotUserInfoService> userInfoServiceMock;
    private Mock<IHttpContextAccessor> httpContextAccessorMock;
    private Context context;
    private GeopilotUserHandler geopilotUserHandler;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<GeopilotUserHandler>>();
        userInfoServiceMock = new Mock<GeopilotUserInfoService>();
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
        var authIdentifier = Guid.NewGuid().ToString();
        var newUser = CreateUser(authIdentifier, "BROOMNEIGHBOR", "ONYXSHADOW@example.com");
        var authHandlerContext = SetupAuthorizationHandlerContext(newUser);

        // Create user
        var user = await geopilotUserHandler.UpdateOrCreateUser(authHandlerContext);
        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("BROOMNEIGHBOR", user.FullName);
        Assert.AreEqual("ONYXSHADOW@example.com", user.Email);
        Assert.AreEqual(false, user.IsAdmin, "Automatically added user should not get admin rights when there are already users in the database.");

        // Update user
        var updatedUser = CreateUser(authIdentifier, "PERFECTSTONE", "DIRERUN@example.com");
        authHandlerContext = SetupAuthorizationHandlerContext(updatedUser);

        user = await geopilotUserHandler.UpdateOrCreateUser(authHandlerContext);
        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("PERFECTSTONE", user.FullName);
        Assert.AreEqual("DIRERUN@example.com", user.Email);
        Assert.AreEqual(false, user.IsAdmin);
    }

    [TestMethod]
    public async Task UpdateOrCreateUserElevatesFirstUserToAdmin()
    {
        var authIdentifier = Guid.NewGuid().ToString();
        var newUser = CreateUser(authIdentifier, "STORMSLAW", "MAIN@example.com");
        var authHandlerContext = SetupAuthorizationHandlerContext(newUser);

        // Clear users with all relations in database, so that the first user is elevated to admin.
        context.Assets.RemoveRange(context.Assets);
        context.Deliveries.RemoveRange(context.Deliveries);
        context.Users.RemoveRange(context.Users);
        context.SaveChanges();

        // Create first user in database.
        var user = await geopilotUserHandler.UpdateOrCreateUser(authHandlerContext);
        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("STORMSLAW", user.FullName);
        Assert.AreEqual("MAIN@example.com", user.Email);
        Assert.AreEqual(true, user.IsAdmin, "First user added to the database should be elevated to admin.");
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

    private AuthorizationHandlerContext SetupAuthorizationHandlerContext(User user)
        => new AuthorizationHandlerContext(Enumerable.Empty<IAuthorizationRequirement>(), CreateClaimsPrincipal(user), null);
}
