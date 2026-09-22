using Geopilot.Api.Authorization;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Geopilot.Api.Test.Authorization;

[TestClass]
public class DeclarerHandlerTest
{
    private Context context;
    private Mock<IGeopilotUserResolver> userResolverMock;
    private DeclarerHandler handler;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        userResolverMock = new Mock<IGeopilotUserResolver>();
        handler = new DeclarerHandler(Mock.Of<ILogger<DeclarerHandler>>(), context, userResolverMock.Object);
    }

    [TestCleanup]
    public void Cleanup() => context.Dispose();

    [TestMethod]
    public async Task AdmitsAnActiveMachineClientWithoutAskingTheIdentityProvider()
    {
        var client = AddMachineClient(MachineClientState.Active);

        var result = await HandleAsync(client.AuthIdentifier);

        Assert.IsTrue(result.HasSucceeded);
        userResolverMock.Verify(r => r.ResolveAsync(), Times.Never, "A client token names no person, and most identity providers describe none for it; asking would refuse every machine.");
    }

    [TestMethod]
    public async Task RefusesAnInactiveMachineClient()
    {
        var client = AddMachineClient(MachineClientState.Inactive);

        var result = await HandleAsync(client.AuthIdentifier);

        Assert.IsTrue(result.HasFailed, "Deactivating a client has to revoke it, like deactivating a user.");
    }

    [TestMethod]
    public async Task AdmitsAnActiveUser()
    {
        var user = new User { AuthIdentifier = Guid.NewGuid().ToString(), State = UserState.Active };
        userResolverMock.Setup(r => r.ResolveAsync()).ReturnsAsync(user);

        var result = await HandleAsync(user.AuthIdentifier);

        Assert.IsTrue(result.HasSucceeded, "Until the machine delivery is restricted to machines, a user delivers to it as well.");
    }

    [TestMethod]
    public async Task RefusesAnInactiveUser()
    {
        var user = new User { AuthIdentifier = Guid.NewGuid().ToString(), State = UserState.Inactive };
        userResolverMock.Setup(r => r.ResolveAsync()).ReturnsAsync(user);

        var result = await HandleAsync(user.AuthIdentifier);

        Assert.IsTrue(result.HasFailed);
    }

    [TestMethod]
    public async Task LeavesAnUnknownSubjectUnauthorized()
    {
        userResolverMock.Setup(r => r.ResolveAsync()).ReturnsAsync(default(User?));

        var result = await HandleAsync(Guid.NewGuid().ToString());

        Assert.IsFalse(result.HasSucceeded, "A valid token whose subject nobody registered is not a client; the administrator decides who delivers.");
    }

    [TestMethod]
    public async Task IgnoresAnUnauthenticatedCaller()
    {
        var authorizationContext = new AuthorizationHandlerContext([new DeclarerRequirement()], new ClaimsPrincipal(new ClaimsIdentity()), null);

        await handler.HandleAsync(authorizationContext);

        Assert.IsFalse(authorizationContext.HasSucceeded);
        userResolverMock.Verify(r => r.ResolveAsync(), Times.Never);
    }

    private MachineClient AddMachineClient(MachineClientState state)
    {
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR", State = state }).Entity;
        context.SaveChanges();
        return client;
    }

    private async Task<AuthorizationHandlerContext> HandleAsync(string subject)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, subject)], "Test"));
        var authorizationContext = new AuthorizationHandlerContext([new DeclarerRequirement()], principal, null);
        await handler.HandleAsync(authorizationContext);
        return authorizationContext;
    }
}
