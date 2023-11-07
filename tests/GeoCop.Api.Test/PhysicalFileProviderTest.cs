using Microsoft.Extensions.Configuration;

namespace GeoCop.Api
{
    [TestClass]
    public sealed class PhysicalFileProviderTest
    {
        private const string JobId = "8c0681a9-6f7e-4fa1-9f46-ec4431414b7f";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void CreateFileWithRandomName()
        {
            var configuration = CreateConfiguration();
            var fileProvider = new PhysicalFileProvider(configuration, "GEOCOP_UPLOADS_DIR");

            fileProvider.Initialize(new Guid(JobId));

            var fileHandle = fileProvider.CreateFileWithRandomName(".xtf");

            Assert.IsNotNull(fileHandle);
            Assert.IsNotNull(fileHandle.FileName);
            Assert.IsNotNull(fileHandle.Stream);

            Assert.AreEqual(".xtf", Path.GetExtension(fileHandle.FileName));
            Assert.IsTrue(fileProvider.Exists(fileHandle.FileName));
            Assert.IsTrue(fileHandle.Stream.CanWrite);
        }

        private IConfiguration CreateConfiguration() =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GEOCOP_UPLOADS_DIR", TestContext.DeploymentDirectory },
            }).Build();
    }
}
