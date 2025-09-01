using Geopilot.Api.Contracts;
using Geopilot.Api.Controllers;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Geopilot.Api.Test.Controllers;

[TestClass]
public class ValidatorControllerTest
{
    [TestMethod]
    public async Task GetReturnsAllValidatorConfigurations()
    {
        var validator1 = new Mock<IValidator>(MockBehavior.Strict);
        validator1.SetupGet(v => v.Name).Returns("VAL1");
        validator1.Setup(v => v.GetSupportedFileExtensionsAsync()).ReturnsAsync(new List<string> { ".xtf" });
        validator1.Setup(v => v.GetSupportedProfilesAsync()).ReturnsAsync(new List<Geowerkstatt.Ilicop.Web.Contracts.Profile>
        {
            new() { Id = "P1" },
        });

        var validator2 = new Mock<IValidator>(MockBehavior.Strict);
        validator2.SetupGet(v => v.Name).Returns("VAL2");
        validator2.Setup(v => v.GetSupportedFileExtensionsAsync()).ReturnsAsync(new List<string> { ".gml", ".*" });
        validator2.Setup(v => v.GetSupportedProfilesAsync()).ReturnsAsync(new List<Geowerkstatt.Ilicop.Web.Contracts.Profile>
        {
            new() { Id = "Q1" },
            new() { Id = "Q2" },
        });

        var controller = new ValidatorController(new[] { validator1.Object, validator2.Object });

        var actionResult = await controller.Get();
        var ok = actionResult as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreEqual(StatusCodes.Status200OK, ok.StatusCode);

        var payload = ok.Value as Dictionary<string, ValidatorConfiguration>;
        Assert.IsNotNull(payload);
        Assert.AreEqual(2, payload.Count);

        Assert.IsTrue(payload.ContainsKey("VAL1"));
        Assert.IsTrue(payload.ContainsKey("VAL2"));

        var val1 = payload["VAL1"];
        CollectionAssert.AreEqual(new List<string> { ".xtf" }, val1.SupportedFileExtensions);
        Assert.AreEqual(1, val1.Profiles.Count);
        Assert.AreEqual("P1", val1.Profiles[0].Id);

        var val2 = payload["VAL2"];
        CollectionAssert.AreEqual(new List<string> { ".gml", ".*" }, val2.SupportedFileExtensions);
        Assert.AreEqual(2, val2.Profiles.Count);
        Assert.AreEqual("Q1", val2.Profiles[0].Id);
        Assert.AreEqual("Q2", val2.Profiles[1].Id);

        validator1.VerifyAll();
        validator2.VerifyAll();
    }
}
