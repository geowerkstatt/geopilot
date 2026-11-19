using Geopilot.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Controllers;

[TestClass]
public class RefMappingControllerTest
{
    private Context context;
    private RefMappingController controller;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        controller = new RefMappingController(Mock.Of<ILogger<RefMappingController>>(), context);
    }

    [TestCleanup]
    public void Cleanup() => context.Dispose();

    [TestMethod]
    public async Task CreateStoresTheTrimmedEntry()
    {
        var created = await CreateAsync(new RefMapping
        {
            Scope = "  351 ",
            RefDataUris = [" ilidata:dmav_LFP3_351 "],
            Comment = "   ",
        });

        Assert.AreEqual("351", created.Scope);
        Assert.AreEqual("ilidata:dmav_LFP3_351", Assert.ContainsSingle(created.RefDataUris));
        Assert.IsNull(created.Comment);
    }

    [TestMethod]
    public async Task EditUpdatesAllFields()
    {
        var created = await CreateAsync(NewEntry());

        var changed = new RefMapping
        {
            Id = created.Id,
            Scope = "512",
            RefDataUris = ["ilidata:dmav_HFP1_V1_0", "ilidata:dmav_LFP1_V1_0"],
            Comment = "Cham",
        };

        var result = await controller.Edit(changed, CancellationToken.None);

        ActionResultAssert.IsOkObjectResult<RefMapping>(result);
        var stored = context.RefMappings.AsNoTracking().Single(r => r.Id == created.Id);
        Assert.AreEqual("512", stored.Scope);
        CollectionAssert.AreEqual(new[] { "ilidata:dmav_HFP1_V1_0", "ilidata:dmav_LFP1_V1_0" }, stored.RefDataUris);
        Assert.AreEqual("Cham", stored.Comment);
    }

    [TestMethod]
    public async Task EditRefusesAnInvalidEntry()
    {
        var created = await CreateAsync(NewEntry());

        var result = await controller.Edit(new RefMapping { Id = created.Id, Scope = "351", RefDataUris = ["/etc/passwd"] }, CancellationToken.None);

        ActionResultAssert.IsBadRequest(result);
        Assert.AreEqual("ilidata:dmav_LFP3_351", context.RefMappings.AsNoTracking().Single(r => r.Id == created.Id).RefDataUris.Single());
    }

    [TestMethod]
    public async Task CreateWithDuplicateScopeReturnsConflict()
    {
        await CreateAsync(NewEntry());

        var result = await controller.Create(NewEntry(), CancellationToken.None);

        ActionResultAssert.IsConflict(result, "351");
    }

    [TestMethod]
    public async Task EditToDuplicateScopeReturnsConflict()
    {
        await CreateAsync(NewEntry());
        var other = await CreateAsync(new RefMapping { Scope = "512", RefDataUris = ["ilidata:dmav_LFP3_512"] });

        var result = await controller.Edit(NewEntry(other.Id), CancellationToken.None);

        ActionResultAssert.IsConflict(result, "351");
    }

    [TestMethod]
    public async Task EditReturnsNotFoundForAnUnknownEntry()
    {
        ActionResultAssert.IsNotFound(await controller.Edit(NewEntry(int.MaxValue), CancellationToken.None));
    }

    [TestMethod]
    public async Task DeleteRemovesTheEntry()
    {
        var created = await CreateAsync(NewEntry());

        Assert.IsInstanceOfType<NoContentResult>(await controller.Delete(created.Id, CancellationToken.None));
        ActionResultAssert.IsNotFound(await controller.GetById(created.Id, CancellationToken.None));
    }

    [TestMethod]
    public async Task DeleteReturnsNotFoundForAnUnknownEntry()
    {
        ActionResultAssert.IsNotFound(await controller.Delete(int.MaxValue, CancellationToken.None));
    }

    [TestMethod]
    public async Task GetReturnsTheCreatedEntry()
    {
        var created = await CreateAsync(NewEntry());

        var result = Assert.IsInstanceOfType<OkObjectResult>(await controller.Get(CancellationToken.None));

        var entries = Assert.IsInstanceOfType<IEnumerable<RefMapping>>(result.Value);

        Assert.IsTrue(entries.Any(r => r.Id == created.Id));
    }

    [TestMethod]
    [DataRow("351", "ilidata:dmav_LFP3_351")]
    public void ValidateAcceptsAValidEntry(string scope, string uri)
    {
        Assert.IsNull(RefMappingController.Validate(new RefMapping { Scope = scope, RefDataUris = [uri] }));
    }

    [TestMethod]
    [DataRow(null, "ilidata:dmav_LFP3_351", DisplayName = "null scope")]
    [DataRow(" ", "ilidata:dmav_LFP3_351", DisplayName = "blank scope")]
    [DataRow("35\u000b1", "ilidata:dmav_LFP3_351", DisplayName = "invalid character in scope")]
    [DataRow("351", "https://example.ch/lfp3.xtf", DisplayName = "http reference")]
    [DataRow("351", "C:\\data\\lfp3.xtf", DisplayName = "local windows path")]
    [DataRow("351", "/data/lfp3.xtf", DisplayName = "local unix path")]
    [DataRow("351", "ilidata:../lfp3.xtf", DisplayName = "path inside ilidata")]
    [DataRow("351", "ilidata:", DisplayName = "ilidata without id")]
    [DataRow("351", "ILIDATA:dmav_LFP3_351", DisplayName = "upper case prefix")]
    public void ValidateRefusesAnInvalidEntry(string? scope, string uri)
    {
        Assert.IsNotNull(RefMappingController.Validate(new RefMapping { Scope = scope!, RefDataUris = [uri] }));
    }

    [TestMethod]
    public void ValidateRefusesAnInvalidComment()
    {
        Assert.IsNotNull(RefMappingController.Validate(new RefMapping { Scope = "351", RefDataUris = ["ilidata:dmav_LFP3_351"], Comment = "Invalid\u000bcomment" }));
    }

    [TestMethod]
    public void ValidateRefusesAnEntryWithoutReference()
    {
        Assert.IsNotNull(RefMappingController.Validate(new RefMapping { Scope = "351" }));
    }

    [TestMethod]
    public void ValidateRefusesAnyInvalidReferenceAmongValidOnes()
    {
        Assert.IsNotNull(RefMappingController.Validate(new RefMapping { Scope = "351", RefDataUris = ["ilidata:dmav_LFP3_351", "file:///etc/passwd"] }));
    }

    [TestMethod]
    public void ValidateRefusesScopeLongerThan250Characters()
    {
        Assert.IsNotNull(RefMappingController.Validate(new RefMapping { Scope = new string('1', 251), RefDataUris = ["ilidata:dmav_LFP3_351"] }));
        Assert.IsNull(RefMappingController.Validate(new RefMapping { Scope = new string('1', 250), RefDataUris = ["ilidata:dmav_LFP3_351"] }));
    }

    private async Task<RefMapping> CreateAsync(RefMapping refMapping)
    {
        var result = await controller.Create(refMapping, CancellationToken.None);
        ActionResultAssert.IsCreated(result);
        return Assert.IsInstanceOfType<RefMapping>((result as CreatedResult)?.Value);
    }

    private static RefMapping NewEntry(int id = 0) => new()
    {
        Id = id,
        Scope = "351",
        RefDataUris = ["ilidata:dmav_LFP3_351"],
    };
}
