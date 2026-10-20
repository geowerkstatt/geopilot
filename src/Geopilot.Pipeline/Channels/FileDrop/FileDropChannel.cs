using System.Globalization;

namespace Geopilot.Pipeline.Channels.FileDrop;

/// <summary>
/// Places the files of a delivery in a folder of their own, <c>{baseDirectory}/{subfolder}/{delivery id}</c>, where
/// the systems of the recipient pick them up. The host provides the base directory, the mandate the subfolder. The
/// files go to a temporary folder that is renamed at the end, so the target never shows half a delivery, and a target
/// that already exists counts as delivered.
/// </summary>
internal sealed class FileDropChannel : IDeliveryChannel
{
    /// <summary>
    /// The name of the host value with the base directory, below <c>Pipeline:ChannelConfigs</c>.
    /// </summary>
    internal const string BaseDirectoryKey = "baseDirectory";

    private readonly string baseDirectory;
    private readonly string mandateDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileDropChannel"/> class.
    /// </summary>
    /// <param name="baseDirectory">The directory the host provides for all file drops.</param>
    /// <param name="subfolder">The folder of the mandate, a relative path below the base directory.</param>
    /// <exception cref="ArgumentException">The subfolder does not lie below the base directory.</exception>
    public FileDropChannel(string baseDirectory, [MandateValue] string subfolder)
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

        var deliveryFolder = delivery.Id.ToString(CultureInfo.InvariantCulture);
        var target = Path.Combine(mandateDirectory, deliveryFolder);
        if (Directory.Exists(target))
            return ChannelResult.Success($"The delivery already is in <{target}>.");

        // The leading dot lets whoever reads the drop skip it.
        var partial = Path.Combine(mandateDirectory, $".{deliveryFolder}.partial");
        var delivered = false;
        try
        {
            // An attempt whose instance stopped leaves its temporary folder behind.
            if (Directory.Exists(partial))
                Directory.Delete(partial, recursive: true);

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

        var resolved = Path.GetFullPath(Path.Combine(baseDirectory, subfolder));
        if (!resolved.StartsWith(Path.TrimEndingDirectorySeparator(baseDirectory) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"The subfolder <{subfolder}> does not lie below the base directory.", nameof(subfolder));

        return resolved;
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
