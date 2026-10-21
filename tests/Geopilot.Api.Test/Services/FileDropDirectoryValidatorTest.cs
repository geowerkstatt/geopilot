using Geopilot.Api.FileAccess;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Config;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class FileDropDirectoryValidatorTest
{
    private const string FileDropKey = "Pipeline:ChannelConfigs:Geopilot.Pipeline.Channels.FileDrop.FileDropChannel:baseDirectory";

    private string root;

    [TestInitialize]
    public void Initialize()
    {
        // Pure path arithmetic, the validator never touches the disk, so nothing is created or cleaned up.
        root = Path.Combine(Path.GetTempPath(), $"geopilot-file-drop-guard-test-{Guid.NewGuid():N}");
    }

    [TestMethod]
    public void AcceptsABaseDirectoryOfItsOwn()
    {
        Assert.IsTrue(Validate(Path.Combine(root, "drop")).Succeeded);
    }

    [TestMethod]
    public void AcceptsAnInstallationWithoutFileDrop()
    {
        Assert.IsTrue(Validate(baseDirectory: null).Succeeded);
    }

    [TestMethod]
    public void RejectsABaseDirectoryInsideAStorageDirectory()
    {
        var result = Validate(Path.Combine(root, "assets", "drop"));

        Assert.IsTrue(result.Failed);
        Assert.Contains(FileDropKey, result.FailureMessage);
        Assert.Contains("Storage:AssetsDirectory", result.FailureMessage);
    }

    [TestMethod]
    public void RejectsAStorageDirectoryInsideTheBaseDirectory()
    {
        // The other direction leaks: whoever reads the drop would see the data of geopilot.
        var result = Validate(root);

        Assert.IsTrue(result.Failed);
    }

    [TestMethod]
    public void RejectsTheDirectUploadDirectory()
    {
        var uploads = Path.Combine(root, "uploads");

        var result = Validate(Path.Combine(uploads, "drop"), uploads);

        Assert.IsTrue(result.Failed);
        Assert.Contains("Upload:Direct:Directory", result.FailureMessage);
    }

    private ValidateOptionsResult Validate(string? baseDirectory, string? uploadDirectory = null)
    {
        var storage = new FileAccessOptions
        {
            DownloadDirectory = Path.Combine(root, "downloads"),
            VisualizationDirectory = Path.Combine(root, "visualizations"),
            AssetsDirectory = Path.Combine(root, "assets"),
            PipelineDirectory = Path.Combine(root, "pipeline"),
            ResourcesDirectory = Path.Combine(root, "resources"),
        };
        var upload = new UploadDirectOptions { Directory = uploadDirectory ?? string.Empty };
        var pipeline = new PipelineOptions { Definition = "pipeline.yaml" };
        if (baseDirectory is not null)
            pipeline.ChannelConfigs["Geopilot.Pipeline.Channels.FileDrop.FileDropChannel"] = new Parameterization { ["baseDirectory"] = baseDirectory };

        return new FileDropDirectoryValidator(Options.Create(storage), Options.Create(upload)).Validate(null, pipeline);
    }
}
