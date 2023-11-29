using GeoCop.Api.Test;

namespace GeoCop.Api.FileAccess;

[TestClass]
public sealed class PhysicalFileProviderTest
{
    private const string JobId = "8c0681a9-6f7e-4fa1-9f46-ec4431414b7f";

    [TestMethod]
    public void CreateFileWithRandomName()
    {
        var fileProvider = new PhysicalFileProvider(Initialize.TestDirectoryProvider);

        fileProvider.Initialize(new Guid(JobId));

        var fileHandle = fileProvider.CreateFileWithRandomName(".xtf");

        Assert.IsNotNull(fileHandle);
        Assert.IsNotNull(fileHandle.FileName);
        Assert.IsNotNull(fileHandle.Stream);

        Assert.AreEqual(".xtf", Path.GetExtension(fileHandle.FileName));
        Assert.IsTrue(fileProvider.Exists(fileHandle.FileName));
        Assert.IsTrue(fileHandle.Stream.CanWrite);
    }
}
