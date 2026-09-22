using Geopilot.Api.FileAccess;
using Microsoft.Extensions.Options;
using System.Reflection;

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

        // Read the directories off the type instead of listing them here. A listing is complete only
        // until someone adds a sixth directory, and what it costs to miss one is deleted data. The
        // static SectionName is not an instance property and stays out by itself.
        var storage = fileAccessOptions.Value;
        var storageDirectories = typeof(FileAccessOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string) && property.CanRead)
            .Select(property => (Key: property.Name, Directory: property.GetValue(storage) as string));

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
