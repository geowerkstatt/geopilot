using Geopilot.Api.FileAccess;
using Geopilot.Api.Services;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class UploadDirectDirectoryValidatorTest
{
    private string root;

    [TestInitialize]
    public void Initialize()
    {
        // Pure path arithmetic, the validator never touches the disk, so nothing is created or cleaned up.
        root = Path.Combine(Path.GetTempPath(), $"geopilot-upload-guard-test-{Guid.NewGuid():N}");
    }

    [TestMethod]
    public void AcceptsDirectoryOutsideTheStorageDirectories()
    {
        var result = Validate(Path.Combine(root, "uploads"), Path.Combine(root, "pipeline"));

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public void RejectsDirectoryEqualToAStorageDirectory()
    {
        var shared = Path.Combine(root, "data");

        var result = Validate(shared, shared);

        Assert.IsTrue(result.Failed);
        var message = result.FailureMessage;
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "Upload:Direct:Directory");
        StringAssert.Contains(message, "Storage:PipelineDirectory");
    }

    [TestMethod]
    public void RejectsDirectoryInsideAStorageDirectory()
    {
        var result = Validate(Path.Combine(root, "data", "uploads"), Path.Combine(root, "data"));

        Assert.IsTrue(result.Failed);
    }

    [TestMethod]
    public void RejectsStorageDirectoryInsideTheUploadDirectory()
    {
        // The other direction is destructive too: the upload cleanup deletes what it cannot account for.
        var result = Validate(Path.Combine(root, "data"), Path.Combine(root, "data", "pipeline"));

        Assert.IsTrue(result.Failed);
    }

    [TestMethod]
    public void AcceptsNeighbourWhoseNameStartsWithTheUploadDirectory()
    {
        var result = Validate(Path.Combine(root, "data"), Path.Combine(root, "database"));

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public void AcceptsMissingDirectoryBecauseTheDataAnnotationReportsIt()
    {
        var result = Validate(string.Empty, Path.Combine(root, "pipeline"));

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public void RejectsOverlapWithAnyStorageDirectory()
    {
        // The other tests all collide with the pipeline directory. This one pins that the check covers
        // every directory of the section, not the one that happens to be parameterized.
        var shared = Path.Combine(root, "assets");
        var storage = new FileAccessOptions
        {
            DownloadDirectory = Path.Combine(root, "downloads"),
            VisualizationDirectory = Path.Combine(root, "visualizations"),
            AssetsDirectory = shared,
            PipelineDirectory = Path.Combine(root, "pipeline"),
            ResourcesDirectory = Path.Combine(root, "resources"),
        };

        var result = new UploadDirectDirectoryValidator(Options.Create(storage))
            .Validate(null, new UploadDirectOptions { Directory = shared });

        Assert.IsTrue(result.Failed);
        var message = result.FailureMessage;
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "Storage:AssetsDirectory");
    }

    private ValidateOptionsResult Validate(string uploadDirectory, string pipelineDirectory)
    {
        var storage = new FileAccessOptions
        {
            DownloadDirectory = Path.Combine(root, "downloads"),
            VisualizationDirectory = Path.Combine(root, "visualizations"),
            AssetsDirectory = Path.Combine(root, "assets"),
            PipelineDirectory = pipelineDirectory,
            ResourcesDirectory = Path.Combine(root, "resources"),
        };

        var validator = new UploadDirectDirectoryValidator(Options.Create(storage));
        return validator.Validate(null, new UploadDirectOptions { Directory = uploadDirectory });
    }
}
