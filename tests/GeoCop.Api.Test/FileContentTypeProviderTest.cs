using GeoCop.Api.StacServices;

namespace GeoCop.Api.Test
{
    [TestClass]
    public class FileContentTypeProviderTest
    {
        private FileContentTypeProvider fileContentTypeProvider;
        [TestInitialize]
        public void Initialize()
        {
            fileContentTypeProvider = new FileContentTypeProvider();
        }

        [TestCleanup]
        public void Cleanup()
        {
        }

        [TestMethod]
        [DataRow(".xtf", "application/interlis+xml")]
        public void GetContentType(string fileExtension, string expectedMediaType)
        {
            var contentType = fileContentTypeProvider.GetContentType(fileExtension);
            Assert.AreEqual(expectedMediaType, contentType.MediaType);
        }
    }
}
