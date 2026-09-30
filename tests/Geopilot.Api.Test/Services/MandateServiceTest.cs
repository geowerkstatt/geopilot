using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline.Config;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class MandateServiceTest
{
    private Context context;
    private MandateService mandateService;
    private UploadStore uploadStore;
    private User editUser;
    private User adminUser;
    private Mandate unrestrictedMandate;
    private Mandate noDeliveryMandate;
    private Mandate xtfMandate;
    private Mandate publicCsvMandate;
    private Mandate noOrganisationsMandate;
    private Mandate noPermissionMandate;
    private Mandate missingPipelineMandate;
    private Organisation organisation;
    private Mock<IPipelineService> pipelineServiceMock;

    [TestInitialize]
    public void Initialize()
    {
        const string existingPipelineId = "existing-pipeline";
        const string missingPipelineId = "missing-pipeline";

        context = AssemblyInitialize.DbFixture.GetTestContext();
        uploadStore = new UploadStore();

        var pipelineConfig = new PipelineConfig
        {
            Id = existingPipelineId,
            DisplayName = new Dictionary<string, string> { { "en", "Existing Pipeline" } },
            Steps = [],
        };
        pipelineServiceMock = new Mock<IPipelineService>();
        pipelineServiceMock.Setup(s => s.GetAvailablePipelines()).Returns([pipelineConfig]);

        mandateService = new MandateService(context, uploadStore, pipelineServiceMock.Object);

        unrestrictedMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = TestHelpers.Localized(nameof(unrestrictedMandate)), AllowDelivery = true, PipelineId = existingPipelineId };
        noDeliveryMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = TestHelpers.Localized(nameof(noDeliveryMandate)), AllowDelivery = false, PipelineId = existingPipelineId };
        xtfMandate = new Mandate { FileTypes = new string[] { ".xtf" }, Name = TestHelpers.Localized(nameof(xtfMandate)), AllowDelivery = true, PipelineId = existingPipelineId };
        publicCsvMandate = new Mandate { FileTypes = new string[] { ".csv" }, Name = TestHelpers.Localized(nameof(publicCsvMandate)), IsPublic = true, AllowDelivery = true, PipelineId = existingPipelineId };
        noOrganisationsMandate = new Mandate { FileTypes = new string[] { ".itf" }, Name = TestHelpers.Localized(nameof(noOrganisationsMandate)), AllowDelivery = true, PipelineId = existingPipelineId };
        noPermissionMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = TestHelpers.Localized(nameof(noPermissionMandate)), AllowDelivery = true, PipelineId = existingPipelineId };
        missingPipelineMandate = new Mandate { FileTypes = new string[] { ".*" }, Name = TestHelpers.Localized(nameof(missingPipelineMandate)), AllowDelivery = true, PipelineId = missingPipelineId };

        context.Mandates.Add(unrestrictedMandate);
        context.Mandates.Add(noDeliveryMandate);
        context.Mandates.Add(xtfMandate);
        context.Mandates.Add(publicCsvMandate);
        context.Mandates.Add(noOrganisationsMandate);
        context.Mandates.Add(noPermissionMandate);
        context.Mandates.Add(missingPipelineMandate);

        editUser = CreateUser("ms-123", "Edit User", "example@example.org");
        context.Users.Add(editUser);

        adminUser = CreateUser("ms-1234", "Admin User", "admin.example@example.org", isAdmin: true);
        context.Users.Add(adminUser);

        organisation = new Organisation { Name = "GAMMAHUNT" };
        organisation.Mandates.Add(unrestrictedMandate);
        organisation.Mandates.Add(noDeliveryMandate);
        organisation.Mandates.Add(xtfMandate);
        organisation.Mandates.Add(publicCsvMandate);
        organisation.Mandates.Add(missingPipelineMandate);
        organisation.Users.Add(editUser);
        organisation.Users.Add(adminUser);

        var organisation2 = new Organisation { Name = "DELTALIGHT" };
        organisation2.Mandates.Add(noPermissionMandate);
        organisation2.Users.Add(adminUser);

        context.Add(organisation);
        context.Add(organisation2);
        context.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup()
    {
        context.Dispose();
    }

    private MachineClient AddMachineClient(bool memberOfOrganisation)
    {
        var client = new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "SILENTHARBOR" };
        if (memberOfOrganisation)
            organisation.MachineClients.Add(client);
        else
            context.MachineClients.Add(client);

        context.SaveChanges();
        return client;
    }

    [TestMethod]
    public async Task GetMandateKeysReturnsOnlyMandatesThatCarryAKey()
    {
        xtfMandate.Key = "GRUMPYFALCON";
        publicCsvMandate.Key = "SOMBERSPORK";
        context.SaveChanges();

        var keys = await mandateService.GetMandateKeysAsync();

        // The seeded mandates carry keys of their own, so this counts instead of comparing a fixed set.
        CollectionAssert.Contains(keys, "GRUMPYFALCON");
        CollectionAssert.Contains(keys, "SOMBERSPORK");
        Assert.HasCount(context.Mandates.Count(m => m.Key != null), keys, "Only mandates with a key belong in the result.");
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsPublicMandateForAuthenticatedUser()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        var publicMandate = context.Mandates.Add(new Mandate { Name = TestHelpers.Localized("Public Mandate"), IsPublic = true }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForDeclarerAsync(publicMandate.Id, Declarer.ForUser(user.Id));

        Assert.IsNotNull(result);
        Assert.AreEqual(publicMandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsPublicMandateForUnauthenticatedUser()
    {
        var publicMandate = context.Mandates.Add(new Mandate { Name = TestHelpers.Localized("Public Mandate"), IsPublic = true }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForDeclarerAsync(publicMandate.Id, null);

        Assert.IsNotNull(result);
        Assert.AreEqual(publicMandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsNullForNonPublicMandateWhenUnauthenticated()
    {
        var privateMandate = context.Mandates.Add(new Mandate { Name = TestHelpers.Localized("Private Mandate"), IsPublic = false }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForDeclarerAsync(privateMandate.Id, null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsMandateForAuthorizedUser()
    {
        var (user, mandate) = context.AddMandateWithUserOrganisation();

        var result = await mandateService.GetMandateForDeclarerAsync(mandate.Id, Declarer.ForUser(user.Id));

        Assert.IsNotNull(result);
        Assert.AreEqual(mandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsNullForUnauthorizedUser()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        var mandate = context.Mandates.Add(new Mandate { Name = TestHelpers.Localized("Restricted Mandate"), IsPublic = false }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForDeclarerAsync(mandate.Id, Declarer.ForUser(user.Id));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetMandateAsUserReturnsNullForNonExistentMandate()
    {
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        context.SaveChanges();

        var result = await mandateService.GetMandateForDeclarerAsync(int.MaxValue, Declarer.ForUser(user.Id));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetMandateAsClientReturnsMandateOfTheClientsOrganisation()
    {
        var client = AddMachineClient(memberOfOrganisation: true);

        var result = await mandateService.GetMandateForDeclarerAsync(xtfMandate.Id, Declarer.ForClient(client.Id));

        Assert.IsNotNull(result);
        Assert.AreEqual(xtfMandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateAsClientReturnsNullForMandateOfAnotherOrganisation()
    {
        var client = AddMachineClient(memberOfOrganisation: true);

        var result = await mandateService.GetMandateForDeclarerAsync(noPermissionMandate.Id, Declarer.ForClient(client.Id));

        Assert.IsNull(result, "A client reaches the mandates of its organisations only, like a user.");
    }

    [TestMethod]
    public async Task GetMandateAsClientReturnsPublicMandateWithoutAnyOrganisation()
    {
        var client = AddMachineClient(memberOfOrganisation: false);

        var result = await mandateService.GetMandateForDeclarerAsync(publicCsvMandate.Id, Declarer.ForClient(client.Id));

        Assert.IsNotNull(result, "A public mandate takes deliveries from anyone, a machine included.");
    }

    [TestMethod]
    public async Task GetMandatesReturnsAll()
    {
        var result = await mandateService.GetMandatesAsync();

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        ContainsMandate(result, publicCsvMandate);
        ContainsMandate(result, noOrganisationsMandate);
        ContainsMandate(result, noPermissionMandate);
        ContainsMandate(result, missingPipelineMandate);
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUploadIdAsNonAdmin()
    {
        var uploadId = CreateUpload("Original.xtf");

        var result = await mandateService.GetMandateSummariesAsync(editUser, uploadId);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        DoesNotContainMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, noPermissionMandate);
        DoesNotContainMandate(result, missingPipelineMandate);
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUploadIdAsAdmin()
    {
        var uploadId = CreateUpload("Original.xtf");

        var result = await mandateService.GetMandateSummariesAsync(adminUser, uploadId);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        ContainsMandate(result, noPermissionMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, missingPipelineMandate);
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUploadIdAsUnauthenticated()
    {
        var uploadId = CreateUpload("Original.xtf");

        var result = await mandateService.GetMandateSummariesAsync(null, uploadId);

        DoesNotContainMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, unrestrictedMandate);
        DoesNotContainMandate(result, noDeliveryMandate);
        DoesNotContainMandate(result, xtfMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, noPermissionMandate);
        DoesNotContainMandate(result, missingPipelineMandate);
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUploadIdAsUnauthenticatedReturnsPublic()
    {
        var uploadId = CreateUpload("Original.csv");

        var result = await mandateService.GetMandateSummariesAsync(null, uploadId);

        ContainsMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, unrestrictedMandate);
        DoesNotContainMandate(result, noDeliveryMandate);
        DoesNotContainMandate(result, xtfMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, noPermissionMandate);
        DoesNotContainMandate(result, missingPipelineMandate);
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUploadIdIgnoresCase()
    {
        var uploadId = CreateUpload("Original.XTF");

        var result = await mandateService.GetMandateSummariesAsync(editUser, uploadId);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        DoesNotContainMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, missingPipelineMandate);
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUnknownUploadIdThrows()
    {
        var unknownUploadId = Guid.NewGuid();

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await mandateService.GetMandateSummariesAsync(editUser, unknownUploadId));
    }

    [TestMethod]
    public async Task GetMandateSummariesWithDefaultUploadIdThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await mandateService.GetMandateSummariesAsync(editUser, Guid.Empty));
    }

    [TestMethod]
    public async Task GetMandateSummariesWithUploadWithoutFileExtensionsThrows()
    {
        var uploadId = CreateUpload("noextension");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await mandateService.GetMandateSummariesAsync(editUser, uploadId));
    }

    [TestMethod]
    public async Task GetMandateSummariesWithoutUploadIdSkipsTheFileFilter()
    {
        var result = await mandateService.GetMandateSummariesAsync(editUser, null);

        ContainsMandate(result, unrestrictedMandate);
        ContainsMandate(result, noDeliveryMandate);
        ContainsMandate(result, xtfMandate);
        ContainsMandate(result, publicCsvMandate);
        DoesNotContainMandate(result, noOrganisationsMandate);
        DoesNotContainMandate(result, noPermissionMandate);
        DoesNotContainMandate(result, missingPipelineMandate);
    }

    [TestMethod]
    public async Task GetMandateByKeyReturnsTheAccessibleMandate()
    {
        xtfMandate.Key = "GRUMPYFALCON";
        context.SaveChanges();

        var result = await mandateService.GetMandateByKeyAsync("GRUMPYFALCON", Declarer.ForUser(editUser.Id));

        Assert.IsNotNull(result);
        Assert.AreEqual(xtfMandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetMandateByKeyComparesTheKeyExactly()
    {
        xtfMandate.Key = "GRUMPYFALCON";
        context.SaveChanges();

        Assert.IsNull(await mandateService.GetMandateByKeyAsync("grumpyfalcon", Declarer.ForUser(editUser.Id)), "The key is compared exactly, so a differently cased key must not address the mandate.");
    }

    [TestMethod]
    public async Task GetMandateByKeyHidesAMandateTheUserCannotAccess()
    {
        noPermissionMandate.Key = "SOMBERSPORK";
        context.SaveChanges();

        Assert.IsNull(await mandateService.GetMandateByKeyAsync("SOMBERSPORK", Declarer.ForUser(editUser.Id)));
    }

    [TestMethod]
    public async Task GetMandateByKeyIgnoresSurroundingWhitespace()
    {
        xtfMandate.Key = "GRUMPYFALCON";
        context.SaveChanges();

        var result = await mandateService.GetMandateByKeyAsync("  GRUMPYFALCON\n", Declarer.ForUser(editUser.Id));

        Assert.IsNotNull(result, "The key is trimmed when it is stored, so a key read from a config file must not miss its mandate over a trailing newline.");
        Assert.AreEqual(xtfMandate.Id, result.Id);
    }

    [TestMethod]
    public async Task GetDeliverabilityAsyncRefusesAMandateThatTakesNoDeliveries()
    {
        var result = await mandateService.GetDeliverabilityAsync(noDeliveryMandate, uploadId: null);

        Assert.AreEqual(MandateDeliverability.DeliveryNotAllowed, result);
    }

    [TestMethod]
    public async Task GetDeliverabilityAsyncRefusesAMandateWhosePipelineThisInstallationDoesNotOffer()
    {
        var result = await mandateService.GetDeliverabilityAsync(missingPipelineMandate, uploadId: null);

        Assert.AreEqual(MandateDeliverability.PipelineNotConfigured, result, "A mandate naming an unknown pipeline is a misconfiguration of the installation, not a bad request.");
    }

    [TestMethod]
    public async Task GetDeliverabilityAsyncAcceptsAMandateWithoutAnUpload()
    {
        var result = await mandateService.GetDeliverabilityAsync(xtfMandate, uploadId: null);

        Assert.AreEqual(MandateDeliverability.Deliverable, result, "Without an upload there are no file types to check, and the remaining rules hold.");
    }

    [TestMethod]
    public async Task GetDeliverabilityAsyncRefusesAnUploadTheMandateDoesNotAccept()
    {
        var uploadId = Guid.NewGuid();
        uploadStore.CreateUpload(uploadId, ImmutableList.Create(new UploadedFileInfo("data.csv", $"uploads/{uploadId}/data.csv", 1)));

        var result = await mandateService.GetDeliverabilityAsync(xtfMandate, uploadId);

        Assert.AreEqual(MandateDeliverability.FilesNotAccepted, result);
    }

    [TestMethod]
    public async Task GetDeliverabilityAsyncChecksTheMandateBeforeTheFiles()
    {
        var uploadId = Guid.NewGuid();
        uploadStore.CreateUpload(uploadId, ImmutableList.Create(new UploadedFileInfo("data.csv", $"uploads/{uploadId}/data.csv", 1)));

        var result = await mandateService.GetDeliverabilityAsync(noDeliveryMandate, uploadId);

        Assert.AreEqual(
            MandateDeliverability.DeliveryNotAllowed,
            result,
            "A mandate that takes no deliveries at all must say so, rather than complain about the file types of an upload it would never take.");
    }

    [TestMethod]
    [DataRow(".xtf", true, DisplayName = "The type the mandate names")]
    [DataRow(".XTF", true, DisplayName = "The same type in upper case")]
    [DataRow(".csv", false, DisplayName = "A type the mandate does not name")]
    public async Task AcceptsFileExtensionAsyncChecksOneExtension(string extension, bool expected)
    {
        Assert.AreEqual(expected, await mandateService.AcceptsFileExtensionAsync(xtfMandate.Id, extension));
    }

    [TestMethod]
    public async Task AcceptsFileExtensionAsyncAcceptsAnythingForAWildcardMandate()
    {
        Assert.IsTrue(await mandateService.AcceptsFileExtensionAsync(unrestrictedMandate.Id, ".whatever"));
    }

    [TestMethod]
    [DataRow("NOSUCHKEY", DisplayName = "Unknown key")]
    [DataRow("", DisplayName = "Empty key")]
    [DataRow("   ", DisplayName = "Blank key")]
    public async Task GetMandateByKeyReturnsNullWhenNoMandateMatches(string key)
    {
        xtfMandate.Key = "GRUMPYFALCON";
        context.SaveChanges();

        Assert.IsNull(await mandateService.GetMandateByKeyAsync(key, Declarer.ForUser(editUser.Id)));
    }

    private Guid CreateUpload(params string[] fileNames)
    {
        var uploadId = Guid.NewGuid();
        var files = fileNames
            .Select(name => new UploadedFileInfo(name, $"blobs/{name}", 1024))
            .ToImmutableList();
        uploadStore.CreateUpload(uploadId, files);
        return uploadId;
    }

    private void ContainsMandate(IEnumerable<Mandate> mandates, Mandate mandate)
    {
        var found = mandates.FirstOrDefault(m => m.Id == mandate.Id);
        Assert.IsNotNull(found, $"mandate with id '{mandate.Id}' and name '{mandate.Name}' not found");
    }

    private void ContainsMandate(IEnumerable<MandateSummary> mandates, Mandate mandate)
    {
        var found = mandates.FirstOrDefault(m => m.Id == mandate.Id);
        Assert.IsNotNull(found, $"mandate with id '{mandate.Id}' and name '{mandate.Name}' not found");
    }

    private void DoesNotContainMandate(IEnumerable<MandateSummary> mandates, Mandate mandate)
    {
        var found = mandates.FirstOrDefault(m => m.Id == mandate.Id);
        Assert.IsNull(found);
    }
}
