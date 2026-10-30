using Geopilot.Api.FileAccess;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Channels.FileDrop;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Services;

/// <summary>
/// Refuses a base directory of the file drop that overlaps a data directory of geopilot or a directory it serves
/// without signing in. The cleanups of geopilot delete what they cannot account for, so a delivery placed inside one
/// of its directories would vanish; a data directory inside the base directory would be visible to whoever reads the
/// drop; and a drop inside a public directory would hand every delivery to anyone.
/// </summary>
internal sealed class FileDropDirectoryValidator : IValidateOptions<PipelineOptions>
{
    private static readonly string FileDropImplementation = typeof(FileDropChannel).FullName ?? string.Empty;

    private readonly IOptions<FileAccessOptions> fileAccessOptions;
    private readonly IOptions<UploadDirectOptions> uploadDirectOptions;
    private readonly IConfiguration configuration;
    private readonly IWebHostEnvironment environment;

    public FileDropDirectoryValidator(IOptions<FileAccessOptions> fileAccessOptions, IOptions<UploadDirectOptions> uploadDirectOptions, IConfiguration configuration, IWebHostEnvironment environment)
    {
        this.fileAccessOptions = fileAccessOptions;
        this.uploadDirectOptions = uploadDirectOptions;
        this.configuration = configuration;
        this.environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, PipelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.ChannelConfigs.GetValueOrDefault(FileDropImplementation)?.GetValueOrDefault(FileDropChannel.BaseDirectoryKey) is not string baseDirectory
            || string.IsNullOrWhiteSpace(baseDirectory))
        {
            return ValidateOptionsResult.Success;
        }

        // Only bound when uploads go to the direct backend, and empty otherwise.
        var dataDirectories = DirectoryOverlap.StorageDirectories(fileAccessOptions.Value).ToList();
        var uploadDirectory = uploadDirectOptions.Value.Directory;
        if (!string.IsNullOrWhiteSpace(uploadDirectory))
            dataDirectories.Add(($"{UploadDirectOptions.SectionName}:{nameof(UploadDirectOptions.Directory)}", uploadDirectory));

        // Both are served before the authentication runs.
        var publicAssets = configuration["PublicAssetsOverride"];
        if (!string.IsNullOrWhiteSpace(publicAssets))
            dataDirectories.Add(("PublicAssetsOverride", publicAssets));
        if (!string.IsNullOrWhiteSpace(environment.WebRootPath))
            dataDirectories.Add(("the web root", environment.WebRootPath));

        foreach (var (key, directory) in dataDirectories)
        {
            if (!DirectoryOverlap.Overlaps(baseDirectory, directory))
                continue;

            return ValidateOptionsResult.Fail(
                $"Pipeline:ChannelConfigs:{FileDropImplementation}:{FileDropChannel.BaseDirectoryKey} <{Path.GetFullPath(baseDirectory)}> overlaps " +
                $"{key} <{Path.GetFullPath(directory)}>. The cleanups of geopilot delete files they cannot account for, files in a public " +
                "directory are served without signing in, and whoever reads the drop must not see the data of geopilot. Configure separate directories.");
        }

        return ValidateOptionsResult.Success;
    }
}
