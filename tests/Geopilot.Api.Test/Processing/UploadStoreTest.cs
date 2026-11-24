using System.Collections.Immutable;

namespace Geopilot.Api.Processing;

[TestClass]
public class UploadStoreTest
{
    private Context context;
    private UploadStore store;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        store = new UploadStore(context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        context.Dispose();
    }

    [TestMethod]
    public async Task GetUploadAsyncReturnsTheFilesInTheOrderTheyWereRegistered()
    {
        var id = Guid.NewGuid();
        var files = ImmutableList.Create(
            new UploadedFileInfo("b.xtf", $"uploads/{id}/b.xtf", 3),
            new UploadedFileInfo("a.xtf", $"uploads/{id}/a.xtf", 1, "application/interlis+xml"),
            new UploadedFileInfo("c.xtf", $"uploads/{id}/c.xtf", 2));
        await store.CreateUploadAsync(id, files);
        context.ChangeTracker.Clear();

        var upload = await new UploadStore(context).GetUploadAsync(id);

        Assert.IsNotNull(upload);
        Assert.AreEqual(id, upload.Id);
        CollectionAssert.AreEqual(files, upload.Files);
    }

    [TestMethod]
    public async Task CreateUploadAsyncStoresAUtcTimestamp()
    {
        var before = DateTime.UtcNow;
        var id = Guid.NewGuid();
        await store.CreateUploadAsync(id, ImmutableList<UploadedFileInfo>.Empty);
        context.ChangeTracker.Clear();

        var upload = await store.GetUploadAsync(id);

        Assert.IsNotNull(upload);
        Assert.AreEqual(DateTimeKind.Utc, upload.CreatedAt.Kind);
        Assert.IsTrue(upload.CreatedAt >= before.AddSeconds(-1) && upload.CreatedAt <= before.AddSeconds(1), $"CreatedAt {upload.CreatedAt:O} is not close to {before:O}.");
    }

    [TestMethod]
    public async Task GetUploadAsyncReturnsNullForAnUnknownUpload()
    {
        Assert.IsNull(await store.GetUploadAsync(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task RemoveUploadAsyncDeletesTheSessionAndItsFiles()
    {
        var id = Guid.NewGuid();
        await store.CreateUploadAsync(id, ImmutableList.Create(new UploadedFileInfo("data.xtf", $"uploads/{id}/data.xtf", 7)));
        context.ChangeTracker.Clear();

        Assert.IsTrue(await store.RemoveUploadAsync(id));

        Assert.IsNull(await store.GetUploadAsync(id));
        Assert.IsFalse(context.UploadSessionFiles.Any(f => f.UploadSessionId == id), "the files of the session");
        Assert.IsFalse(await store.RemoveUploadAsync(id), "a second removal");
    }

    [TestMethod]
    public async Task GetUploadIdsCreatedBeforeAsyncReturnsOnlyOlderUploads()
    {
        var id = Guid.NewGuid();
        var upload = await store.CreateUploadAsync(id, ImmutableList<UploadedFileInfo>.Empty);

        Assert.Contains(id, await store.GetUploadIdsCreatedBeforeAsync(upload.CreatedAt.AddSeconds(1)));
        Assert.DoesNotContain(id, await store.GetUploadIdsCreatedBeforeAsync(upload.CreatedAt.AddSeconds(-1)));
    }

    // Counts the whole table: host tests running in parallel commit and delete sessions in the same database.
    [TestMethod]
    [DoNotParallelize]
    public async Task GetActiveUploadCountAsyncCountsEverySession()
    {
        var before = await store.GetActiveUploadCountAsync();

        await store.CreateUploadAsync(Guid.NewGuid(), ImmutableList<UploadedFileInfo>.Empty);
        await store.CreateUploadAsync(Guid.NewGuid(), ImmutableList<UploadedFileInfo>.Empty);

        Assert.AreEqual(before + 2, await store.GetActiveUploadCountAsync());
    }
}
