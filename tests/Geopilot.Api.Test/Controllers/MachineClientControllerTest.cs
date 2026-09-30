using Geopilot.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Controllers;

[TestClass]
public class MachineClientControllerTest
{
    private Context context;
    private MachineClientController controller;
    private Organisation organisation;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        controller = new MachineClientController(Mock.Of<ILogger<MachineClientController>>(), context);
        organisation = context.Organisations.Add(new Organisation { Name = "GAMMAHUNT" }).Entity;
        context.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup() => context.Dispose();

    [TestMethod]
    public async Task CreateRegistersTheClientForItsOrganisations()
    {
        var sub = Guid.NewGuid().ToString();

        var result = await controller.Create(NewClient($"  {sub}  ", organisation.Id));

        ActionResultAssert.IsCreated(result);
        var created = Assert.IsInstanceOfType<MachineClient>((result as CreatedResult)?.Value);
        Assert.AreEqual(sub, created.AuthIdentifier, "The identifier is pasted from the identity provider; a stray space would register a client no token can match.");
        Assert.AreEqual(MachineClientState.Active, created.State);
        Assert.HasCount(1, created.Organisations);
        Assert.AreEqual(organisation.Id, created.Organisations[0].Id);
    }

    [TestMethod]
    public async Task CreateRefusesAClientWithoutIdentifier()
    {
        ActionResultAssert.IsBadRequest(await controller.Create(NewClient("   ", organisation.Id)));
    }

    [TestMethod]
    public async Task CreateRefusesAnIdentifierThatIsAlreadyRegistered()
    {
        var sub = Guid.NewGuid().ToString();
        ActionResultAssert.IsCreated(await controller.Create(NewClient(sub, organisation.Id)));

        var result = await controller.Create(NewClient(sub, organisation.Id));

        ActionResultAssert.IsConflict(result);
        var message = Assert.IsInstanceOfType<string>((result as ConflictObjectResult)?.Value);
        Assert.Contains(sub, message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task CreateRefusesTheIdentifierOfAUser()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        context.SaveChanges();

        var result = await controller.Create(NewClient(user.AuthIdentifier, organisation.Id));

        ActionResultAssert.IsConflict(result);
        Assert.IsFalse(
            context.MachineClients.Any(c => c.AuthIdentifier == user.AuthIdentifier),
            "Registering a person's subject as a machine would lock that person out, because a registered client is never a user.");
    }

    [TestMethod]
    public async Task EditUpdatesIdentifierNameStateAndOrganisations()
    {
        var created = await CreateAsync(Guid.NewGuid().ToString());
        var otherOrganisation = context.Organisations.Add(new Organisation { Name = "DELTALIGHT" }).Entity;
        context.SaveChanges();
        var newSub = Guid.NewGuid().ToString();

        var result = await controller.Edit(new MachineClient
        {
            Id = created.Id,
            AuthIdentifier = newSub,
            Name = "SILENTHARBOR",
            State = MachineClientState.Inactive,
            Organisations = [new Organisation { Id = otherOrganisation.Id }],
        });

        var updated = ActionResultAssert.IsOkObjectResult<MachineClient>(result);
        Assert.AreEqual(newSub, updated.AuthIdentifier);
        Assert.AreEqual("SILENTHARBOR", updated.Name);
        Assert.AreEqual(MachineClientState.Inactive, updated.State);
        Assert.HasCount(1, updated.Organisations);
        Assert.AreEqual(otherOrganisation.Id, updated.Organisations[0].Id);
    }

    [TestMethod]
    public async Task EditRefusesAnIdentifierThatIsAlreadyRegistered()
    {
        var taken = await CreateAsync(Guid.NewGuid().ToString());
        var toEdit = await CreateAsync(Guid.NewGuid().ToString());

        toEdit.AuthIdentifier = taken.AuthIdentifier;
        var result = await controller.Edit(toEdit);

        ActionResultAssert.IsConflict(result);
    }

    [TestMethod]
    public async Task EditRefusesTheIdentifierOfAUser()
    {
        // Moving a client onto a person's subject locks that person out just as registering it would, because
        // a registered client is never resolved as a user.
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        context.SaveChanges();
        var toEdit = await CreateAsync(Guid.NewGuid().ToString());

        toEdit.AuthIdentifier = user.AuthIdentifier;
        var result = await controller.Edit(toEdit);

        ActionResultAssert.IsConflict(result);
    }

    [TestMethod]
    public async Task EditReturnsNotFoundForAnUnknownClient()
    {
        ActionResultAssert.IsNotFound(await controller.Edit(NewClient(Guid.NewGuid().ToString(), organisation.Id)));
    }

    [TestMethod]
    public async Task GetByIdReturnsNotFoundForAnUnknownClient()
    {
        ActionResultAssert.IsNotFound(await controller.GetById(int.MaxValue));
    }

    private async Task<MachineClient> CreateAsync(string sub)
    {
        var result = await controller.Create(NewClient(sub, organisation.Id));
        ActionResultAssert.IsCreated(result);
        return Assert.IsInstanceOfType<MachineClient>((result as CreatedResult)?.Value);
    }

    private static MachineClient NewClient(string authIdentifier, int organisationId) => new()
    {
        AuthIdentifier = authIdentifier,
        Name = "Lieferroboter",
        Organisations = [new Organisation { Id = organisationId }],
    };
}
