namespace GeoCop.Api.Controllers
{
    [TestClass]
    public class VersionConstrollerTest
    {
        [TestMethod]
        public void GetVersion()
        {
            var result = new VersionController().Get();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
            Assert.AreEqual("1.0", result);
        }
    }
}
