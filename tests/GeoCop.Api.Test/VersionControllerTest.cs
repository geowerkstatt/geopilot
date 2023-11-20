namespace GeoCop.Api.Controllers
{
    [TestClass]
    public class VersionControllerTest
    {
        [TestMethod]
        public void GetVersion()
        {
            var result = new VersionController().Get();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
            Assert.IsTrue(result.StartsWith("1.0", StringComparison.Ordinal));
        }
    }
}
