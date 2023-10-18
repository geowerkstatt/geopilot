namespace GeoCop.Api.Controllers
{
    [TestClass]
    public class VersionConstrollerTest
    {
        private VersionController controller;

        [TestInitialize]
        public void Initialize()
        {
            controller = new VersionController();
        }

        [TestMethod]
        public void GetVersion()
        {
            var result = controller.Get();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
            Assert.AreEqual("1.0.0", result);
        }
    }
}
