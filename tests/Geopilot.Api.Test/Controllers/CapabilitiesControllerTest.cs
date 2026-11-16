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
            var controller = CreateController(machineDeliveryOptions: new MachineDeliveryOptions { Enabled = enabled });

            var result = controller.Get();

            Assert.AreEqual(enabled, result.MachineDeliveryEnabled);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "ref mapping enabled")]
        [DataRow(false, DisplayName = "ref mapping disabled")]
        public void GetReportsTheConfiguredRefMapping(bool enabled)
        {
            var controller = CreateController(refMappingOptions: new RefMappingOptions { Enabled = enabled });

            var result = controller.Get();

            Assert.AreEqual(enabled, result.RefMappingEnabled);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "delete window closes")]
        [DataRow(false, DisplayName = "delete window never closes")]
        public void GetReportsWhetherAMandateCanChooseAReactionSet(bool windowCloses)
        {
            var deliveryOptions = new DeliveryOptions { UploaderDeleteEnabled = true, DeleteDuration = windowCloses ? TimeSpan.FromHours(2) : null };
            var controller = CreateController(deliveryOptions: deliveryOptions);

            var result = controller.Get();

            Assert.AreEqual(windowCloses, result.ReactionSetsEnabled);
        }

        private static CapabilitiesController CreateController(
            MachineDeliveryOptions? machineDeliveryOptions = null,
            DeliveryOptions? deliveryOptions = null,
            RefMappingOptions? refMappingOptions = null)
            => new(
                Options.Create(machineDeliveryOptions ?? new MachineDeliveryOptions()),
                Options.Create(deliveryOptions ?? new DeliveryOptions()),
                Options.Create(refMappingOptions ?? new RefMappingOptions()));
    }
}
