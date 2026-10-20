using Geopilot.Api.FileAccess;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Channels.FileDrop;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Services;

/// <summary>
/// Refuses a base directory of the file drop that overlaps a data directory of geopilot. The cleanups of geopilot
/// delete what they cannot account for, so a delivery placed inside one of its directories would vanish, and a data
/// directory inside the base directory would be visible to whoever reads the drop.
/// </summary>
internal sealed class FileDropDirectoryValidator : IValidateOptions<PipelineOptions>
{
    private static readonly string FileDropImplementation = typeof(FileDropChannel).FullName ?? string.Empty;

    private readonly IOptions<FileAccessOptions> fileAccessOptions;
    private readonly IOptions<UploadDirectOptions> uploadDirectOptions;

    public FileDropDirectoryValidator(IOptions<FileAccessOptions> fileAccessOptions, IOptions<UploadDirectOptions> uploadDirectOptions)
    {
        this.fileAccessOptions = fileAccessOptions;
        this.uploadDirectOptions = uploadDirectOptions;
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

        foreach (var (key, directory) in dataDirectories)
        {
            if (!DirectoryOverlap.Overlaps(baseDirectory, directory))
                continue;

            return ValidateOptionsResult.Fail(
                $"Pipeline:ChannelConfigs:{FileDropImplementation}:{FileDropChannel.BaseDirectoryKey} <{Path.GetFullPath(baseDirectory)}> overlaps " +
                $"{key} <{Path.GetFullPath(directory)}>. The cleanups of geopilot delete files they cannot account for, and whoever " +
                "reads the drop must not see the data of geopilot. Configure separate directories.");
        }

        return ValidateOptionsResult.Success;
    }
}
