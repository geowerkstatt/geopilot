namespace Geopilot.Api.FileAccess;

[TestClass]
public sealed class PhysicalJobFileStoreTest
{
    private const string JobId = "8c0681a9-6f7e-4fa1-9f46-ec4431414b7f";

    [TestMethod]
    public void CreateOpenAndExistsRoundTrip()
    {
        var store = new PhysicalAssetFileStore(AssemblyInitialize.TestDirectoryProvider);
        var jobId = new Guid(JobId);
        const string fileName = "round-trip.xtf";

        Assert.IsFalse(store.Exists(jobId, fileName));

        using (var stream = store.CreateFile(jobId, fileName))
        {
            stream.WriteByte(0x42);
        }

        Assert.IsTrue(store.Exists(jobId, fileName));
        using (var read = store.OpenFile(jobId, fileName))
        {
            Assert.AreEqual(0x42, read.ReadByte());
            Assert.AreEqual(-1, read.ReadByte());
        }
    }

    [TestMethod]
    public void GetPathReturnsExpectedLocation()
    {
        var store = new PhysicalAssetFileStore(AssemblyInitialize.TestDirectoryProvider);
        var jobId = new Guid(JobId);

        var expected = Path.Combine(AssemblyInitialize.TestDirectoryProvider.GetAssetDirectoryPath(jobId), "x.xtf");
        Assert.AreEqual(expected, store.GetPath(jobId, "x.xtf"));
    }

    [TestMethod]
    public void DeleteJobRemovesEntireDirectory()
    {
        var store = new PhysicalAssetFileStore(AssemblyInitialize.TestDirectoryProvider);
        var jobId = Guid.NewGuid();
        using (var stream = store.CreateFile(jobId, "a.xtf")) stream.WriteByte(0x01);
        using (var stream = store.CreateFile(jobId, "b.log")) stream.WriteByte(0x02);

        Assert.IsTrue(Directory.Exists(AssemblyInitialize.TestDirectoryProvider.GetAssetDirectoryPath(jobId)));
        store.DeleteJob(jobId);
        Assert.IsFalse(Directory.Exists(AssemblyInitialize.TestDirectoryProvider.GetAssetDirectoryPath(jobId)));
    }

    [TestMethod]
    public void UploadAssetAndDownloadStoresUseSeparateRoots()
    {
        var uploadStore = new PhysicalUploadFileStore(AssemblyInitialize.TestDirectoryProvider);
        var assetStore = new PhysicalAssetFileStore(AssemblyInitialize.TestDirectoryProvider);
        var downloadStore = new PhysicalDownloadFileStore(AssemblyInitialize.TestDirectoryProvider);
        var jobId = Guid.NewGuid();

        using (var stream = uploadStore.CreateFile(jobId, "upload.xtf")) stream.WriteByte(0x99);
        using (var stream = assetStore.CreateFile(jobId, "delivery.xtf")) stream.WriteByte(0xAA);
        using (var stream = downloadStore.CreateFile(jobId, "download.log")) stream.WriteByte(0xBB);

        Assert.IsTrue(uploadStore.Exists(jobId, "upload.xtf"));
        Assert.IsFalse(uploadStore.Exists(jobId, "delivery.xtf"));
        Assert.IsFalse(uploadStore.Exists(jobId, "download.log"));
        Assert.IsTrue(assetStore.Exists(jobId, "delivery.xtf"));
        Assert.IsFalse(assetStore.Exists(jobId, "upload.xtf"));
        Assert.IsFalse(assetStore.Exists(jobId, "download.log"));
        Assert.IsTrue(downloadStore.Exists(jobId, "download.log"));
        Assert.IsFalse(downloadStore.Exists(jobId, "upload.xtf"));
        Assert.IsFalse(downloadStore.Exists(jobId, "delivery.xtf"));
    }

    [TestMethod]
    public void ListFilesReturnsEmptyForMissingJob()
    {
        var store = new PhysicalDownloadFileStore(AssemblyInitialize.TestDirectoryProvider);
        Assert.IsEmpty(store.ListFiles(Guid.NewGuid()));
    }

    [TestMethod]
    public void RandomFileNameGeneratorPreservesExtension()
    {
        var name = new RandomFileNameGenerator().CreateRandomName(".xtf");
        Assert.AreEqual(".xtf", Path.GetExtension(name));
    }
}
