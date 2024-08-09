using Geopilot.Api.Authorization;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Authorization;

[TestClass]
public class GeopilotUserHandlerTest
{
    private Mock<ILogger<GeopilotUserHandler>> loggerMock;
    private Context context;
    private GeopilotUserHandler geopilotUserHandler;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<GeopilotUserHandler>>();
        context = AssemblyInitialize.DbFixture.GetTestContext();
        geopilotUserHandler = new GeopilotUserHandler(loggerMock.Object, context);
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
        const string fullName = "BROOMNEIGHBOR";
        const string email = "ONYXSHADOW@example.com";

        var newUser = new User
        {
            AuthIdentifier = authIdentifier,
            FullName = fullName,
            Email = email,
        };

        var authHandlerContext = SetupAuthorizationHandlerContext(newUser);

        // Create user
        var user = await geopilotUserHandler.UpdateOrCreateUser(authHandlerContext);
        Assert.IsNotNull(user);
        Assert.AreEqual(authIdentifier, user.AuthIdentifier);
        Assert.AreEqual("BROOMNEIGHBOR", user.FullName);
        Assert.AreEqual("ONYXSHADOW@example.com", user.Email);
        Assert.AreEqual(false, user.IsAdmin, "Automatically added user should not get admin rights when there are already users in the database.");

        // Update user
        var updatedUser = new User
        {
            AuthIdentifier = authIdentifier,
            FullName = "PERFECTSTONE",
            Email = "DIRERUN@example.com",
        };

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
        const string fullName = "STORMSLAW";
        const string email = "MAIN@example.com";

        var newUser = new User
        {
            AuthIdentifier = authIdentifier,
            FullName = fullName,
            Email = email,
        };

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

    private AuthorizationHandlerContext SetupAuthorizationHandlerContext(User user)
        => new AuthorizationHandlerContext(Enumerable.Empty<IAuthorizationRequirement>(), CreateClaimsPrincipal(user), null);
}
