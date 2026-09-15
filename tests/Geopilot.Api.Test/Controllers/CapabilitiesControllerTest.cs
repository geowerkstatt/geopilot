using Geopilot.Api.Contracts;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Controllers
{
    [TestClass]
    public class CapabilitiesControllerTest
    {
        [TestMethod]
        [DataRow(true, DisplayName = "machine delivery enabled")]
        [DataRow(false, DisplayName = "machine delivery disabled")]
        public void GetReportsTheConfiguredMachineDelivery(bool enabled)
        {
            var controller = new CapabilitiesController(Options.Create(new MachineDeliveryOptions { Enabled = enabled }));

            var result = controller.Get();

            Assert.AreEqual(new CapabilitiesResponse(enabled), result);
        }
    }
}
