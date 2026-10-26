using System.Globalization;

namespace Geopilot.Pipeline.Channels.FileDrop;

/// <summary>
/// Places the files of a delivery in a folder of their own, <c>{baseDirectory}/{subfolder}/{delivery id}</c>, where
/// the systems of the recipient pick them up. The base configuration provides the base directory, the pipeline
/// definition or the mandate the subfolder. The files go to a temporary folder that is renamed at the end, so the
/// target never shows half a delivery, and a target that already holds exactly the files of the delivery counts as
/// delivered.
/// </summary>
internal sealed class FileDropChannel : IDeliveryChannel
{
    /// <summary>
    /// The name of the base configuration value with the base directory, below <c>Pipeline:ChannelConfigs</c>.
    /// </summary>
    internal const string BaseDirectoryKey = "baseDirectory";

    private readonly string baseDirectory;
    private readonly string mandateDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileDropChannel"/> class.
    /// </summary>
    /// <param name="baseDirectory">The directory the base configuration provides for all file drops.</param>
    /// <param name="subfolder">The folder of the deliveries, a relative path below the base directory.</param>
    /// <exception cref="ArgumentException">The subfolder does not lie below the base directory, or one of its segments
    /// consists of digits only.</exception>
    public FileDropChannel([BaseConfigValue] string baseDirectory, [MandateValue] string subfolder)
    {
        this.baseDirectory = Path.GetFullPath(baseDirectory);
        mandateDirectory = ResolveBelow(this.baseDirectory, subfolder);
    }

    /// <inheritdoc />
    public async Task<ChannelResult> DeliverAsync(DeliveryInfo delivery, IReadOnlyList<IDeliveryFile> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(files);

        // Never created here: a missing base directory is a missing mount, and nobody would read what lands next to it.
        if (!Directory.Exists(baseDirectory))
            return ChannelResult.Failure($"The base directory <{baseDirectory}> does not exist.");

        if (FindLinkBelowTheBaseDirectory() is string link)
            return ChannelResult.Failure($"The folder <{link}> is a link, which the file drop does not follow.");

        var deliveryFolder = delivery.Id.ToString(CultureInfo.InvariantCulture);
        var target = Path.Combine(mandateDirectory, deliveryFolder);

        // The leading dot lets whoever reads the drop skip it. Every attempt gets a folder of its own, so an attempt that
        // overran its time limit cannot remove or move the files of the next one.
        var partial = Path.Combine(mandateDirectory, $".{deliveryFolder}.{Guid.NewGuid():N}.partial");
        var delivered = false;
        try
        {
            // An earlier attempt that got through but could not report it left exactly these files. Anything else
            // belongs to another delivery with the same id, after a restored database or from another installation.
            if (Directory.Exists(target))
            {
                return await HoldsTheFilesAsync(target, files, cancellationToken).ConfigureAwait(false)
                    ? ChannelResult.Success($"The delivery already is in <{target}>.")
                    : ChannelResult.Failure($"The folder <{target}> already exists but does not hold the files of this delivery.");
            }

            // An attempt whose instance stopped leaves its temporary folder behind.
            // shortcut: only an attempt of the same delivery removes it, so the folder of a last attempt stays until an
            // admin resets the reaction.
            RemoveTemporaryFoldersOf(deliveryFolder);

            Directory.CreateDirectory(partial);
            foreach (var file in files)
            {
                if (!IsPlainFileName(file.Name))
                    return ChannelResult.Failure($"The file name <{file.Name}> is not a plain file name.");

                await using var source = await file.OpenReadAsync(cancellationToken).ConfigureAwait(false);
                await using var destination = new FileStream(Path.Combine(partial, file.Name), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            Directory.Move(partial, target);
            delivered = true;
            return ChannelResult.Success($"Delivered to <{target}>.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ChannelResult.Failure($"The delivery could not be placed in <{mandateDirectory}>: {ex.Message}");
        }
        finally
        {
            if (!delivered)
                RemoveTemporaryFolder(partial);
        }
    }

    private static string ResolveBelow(string baseDirectory, string subfolder)
    {
        if (string.IsNullOrWhiteSpace(subfolder) || Path.IsPathRooted(subfolder) || subfolder.Split('/', '\\').Contains(".."))
            throw new ArgumentException($"The subfolder <{subfolder}> is not a relative path without '..'.", nameof(subfolder));

        // The folders of the deliveries are named by their id, so a segment of digits could turn one subfolder into the
        // folder of a delivery of another.
        if (subfolder.Split('/', '\\').Any(segment => segment.Length > 0 && segment.All(char.IsAsciiDigit)))
            throw new ArgumentException($"The subfolder <{subfolder}> has a segment of digits only, which is how the folders of the deliveries are named.", nameof(subfolder));

        var resolved = Path.GetFullPath(Path.Combine(baseDirectory, subfolder));
        if (!resolved.StartsWith(Path.TrimEndingDirectorySeparator(baseDirectory) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"The subfolder <{subfolder}> does not lie below the base directory.", nameof(subfolder));

        return resolved;
    }

    // Whoever reads the drop may write to it as well, so a link below the base directory could steer the files into a
    // directory geopilot can write to. The base directory itself may be a link or a mount.
    private string? FindLinkBelowTheBaseDirectory()
    {
        var root = Path.TrimEndingDirectorySeparator(baseDirectory);
        for (var directory = mandateDirectory; directory is not null && directory.Length > root.Length; directory = Path.GetDirectoryName(directory))
        {
            if (Directory.Exists(directory) && new DirectoryInfo(directory).LinkTarget is not null)
                return directory;
        }

        return null;
    }

    private void RemoveTemporaryFoldersOf(string deliveryFolder)
    {
        if (!Directory.Exists(mandateDirectory))
            return;

        foreach (var folder in Directory.EnumerateDirectories(mandateDirectory, $".{deliveryFolder}.*.partial"))
            RemoveTemporaryFolder(folder);
    }

    private static async Task<bool> HoldsTheFilesAsync(string target, IReadOnlyList<IDeliveryFile> files, CancellationToken cancellationToken)
    {
        if (Directory.EnumerateDirectories(target).Any())
            return false;

        var existing = Directory.EnumerateFiles(target).ToDictionary(path => Path.GetFileName(path), path => new FileInfo(path).Length, StringComparer.Ordinal);
        if (existing.Count != files.Count)
            return false;

        foreach (var file in files)
        {
            if (!existing.TryGetValue(file.Name, out var length))
                return false;

            await using var source = await file.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            if (!source.CanSeek || source.Length != length)
                return false;
        }

        return true;
    }

    private static bool IsPlainFileName(string name)
        => !string.IsNullOrWhiteSpace(name) && name != "." && name != ".." && Path.GetFileName(name) == name && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static void RemoveTemporaryFolder(string partial)
    {
        try
        {
            if (Directory.Exists(partial))
                Directory.Delete(partial, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left for the next attempt, which removes it before it writes; the failure itself is reported already.
        }
    }
}
