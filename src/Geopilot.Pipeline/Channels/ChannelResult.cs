namespace Geopilot.Pipeline.Channels;

/// <summary>
/// The outcome of one attempt of a channel.
/// </summary>
/// <param name="Delivered">Whether the delivery was forwarded.</param>
/// <param name="Message">What happened, for the operators. It is stored with the reaction, kept in the execution
/// protocol for its retention and shown to administrators, so it never contains credentials.</param>
internal sealed record ChannelResult(bool Delivered, string Message)
{
    /// <summary>
    /// The delivery was forwarded.
    /// </summary>
    internal static ChannelResult Success(string message) => new(true, message);

    /// <summary>
    /// The attempt failed in an expected way, such as an unreachable target; the reaction tries again later.
    /// </summary>
    internal static ChannelResult Failure(string message) => new(false, message);
}
