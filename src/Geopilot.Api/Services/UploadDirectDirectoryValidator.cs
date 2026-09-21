using Geopilot.Api.FileAccess;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Services;

/// <summary>
/// Refuses a direct upload directory that overlaps one of the <see cref="FileAccessOptions"/> directories.
/// Both cleanups sweep their own root and delete what they cannot account for, so overlapping roots delete
/// each other's data: the job cleanup reads an upload id as the id of a job it no longer knows, and the
/// upload cleanup reads a job directory as a stale upload.
/// </summary>
internal sealed class UploadDirectDirectoryValidator : IValidateOptions<UploadDirectOptions>
{
    private readonly IOptions<FileAccessOptions> fileAccessOptions;

    public UploadDirectDirectoryValidator(IOptions<FileAccessOptions> fileAccessOptions)
    {
        this.fileAccessOptions = fileAccessOptions;
    }

    public ValidateOptionsResult Validate(string? name, UploadDirectOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // A missing directory is reported by the data annotation; every validator runs, so do not fail twice.
        if (string.IsNullOrWhiteSpace(options.Directory))
            return ValidateOptionsResult.Success;

        var storage = fileAccessOptions.Value;
        var storageDirectories = new (string Key, string? Directory)[]
        {
            (nameof(FileAccessOptions.DownloadDirectory), storage.DownloadDirectory),
            (nameof(FileAccessOptions.VisualizationDirectory), storage.VisualizationDirectory),
            (nameof(FileAccessOptions.AssetsDirectory), storage.AssetsDirectory),
            (nameof(FileAccessOptions.PipelineDirectory), storage.PipelineDirectory),
            (nameof(FileAccessOptions.ResourcesDirectory), storage.ResourcesDirectory),
        };

        foreach (var (key, directory) in storageDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Overlaps(options.Directory, directory))
                continue;

            return ValidateOptionsResult.Fail(
                $"{UploadDirectOptions.SectionName}:{nameof(UploadDirectOptions.Directory)} <{Path.GetFullPath(options.Directory)}> overlaps " +
                $"{FileAccessOptions.SectionName}:{key} <{Path.GetFullPath(directory)}>. The upload directory belongs to the direct upload " +
                $"backend alone, because both cleanups delete files they cannot account for. Configure separate directories.");
        }

        return ValidateOptionsResult.Success;
    }

    /// <summary>
    /// True when one path is the other or lies below it. Compared case-insensitively, so a case-sensitive
    /// filesystem may see an overlap reported that it would not have; refusing to start is the harmless side.
    /// </summary>
    private static bool Overlaps(string left, string right)
    {
        var first = WithTrailingSeparator(left);
        var second = WithTrailingSeparator(right);

        return first.StartsWith(second, StringComparison.OrdinalIgnoreCase)
            || second.StartsWith(first, StringComparison.OrdinalIgnoreCase);
    }

    private static string WithTrailingSeparator(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;
}
