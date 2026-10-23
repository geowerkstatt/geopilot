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
            var controller = new CapabilitiesController(Options.Create(new MachineDeliveryOptions { Enabled = enabled }), Options.Create(new DeliveryOptions()));

            var result = controller.Get();

            Assert.AreEqual(enabled, result.MachineDeliveryEnabled);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "delete window closes")]
        [DataRow(false, DisplayName = "delete window never closes")]
        public void GetReportsWhetherAMandateCanChooseAReactionSet(bool windowCloses)
        {
            var deliveryOptions = new DeliveryOptions { UploaderDeleteEnabled = true, DeleteDuration = windowCloses ? TimeSpan.FromHours(2) : null };
            var controller = new CapabilitiesController(Options.Create(new MachineDeliveryOptions()), Options.Create(deliveryOptions));

            var result = controller.Get();

            Assert.AreEqual(windowCloses, result.ReactionSetsEnabled);
        }
    }
}
