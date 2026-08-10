using System.Collections.Immutable;

namespace Geopilot.Api.Processing;

[TestClass]
public class UploadStoreTest
{
    private UploadStore store;

    [TestInitialize]
    public void Initialize()
    {
        store = new UploadStore();
    }

    [TestMethod]
    public void CreateUpload()
    {
        var id = Guid.NewGuid();
        var files = ImmutableList.Create(new CloudFileInfo("test.xtf", "cloud/key/test.xtf", 42));

        var upload = store.CreateUpload(id, files);

        Assert.AreEqual(id, upload.Id);
        Assert.AreSame(files, upload.Files);
        Assert.AreSame(upload, store.GetUpload(id));
    }

    [TestMethod]
    public void CreateUploadSetsUtcCreatedAt()
    {
        var before = DateTime.UtcNow;
        var upload = store.CreateUpload(Guid.NewGuid(), ImmutableList<CloudFileInfo>.Empty);
        var after = DateTime.UtcNow;

        Assert.AreEqual(DateTimeKind.Utc, upload.CreatedAt.Kind);
        Assert.IsTrue(before <= upload.CreatedAt);
        Assert.IsTrue(upload.CreatedAt <= after);
    }

    [TestMethod]
    public void CreateUploadThrowsIfIdAlreadyExists()
    {
        var id = Guid.NewGuid();
        store.CreateUpload(id, ImmutableList<CloudFileInfo>.Empty);

        Assert.ThrowsExactly<InvalidOperationException>(() => store.CreateUpload(id, ImmutableList<CloudFileInfo>.Empty));
    }
}
