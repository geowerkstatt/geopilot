namespace Geopilot.Pipeline.Channels;

/// <summary>
/// A delivered file as a channel sees it: a name and its content, never a path the channel could write to.
/// </summary>
internal interface IDeliveryFile
{
    /// <summary>
    /// The name the file was delivered under, unique within the delivery.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Opens the content of the file for reading.
    /// </summary>
    Task<Stream> OpenReadAsync(CancellationToken cancellationToken);
}
