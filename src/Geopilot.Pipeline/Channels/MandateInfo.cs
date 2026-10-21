using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Channels;

/// <summary>
/// The mandate of a delivery as a channel sees it.
/// </summary>
internal sealed record MandateInfo
{
    /// <summary>
    /// The id of the mandate.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// The name of the mandate in the languages it is maintained in.
    /// </summary>
    public required LocalizedText Name { get; init; }

    /// <summary>
    /// The key machine clients address the mandate by, or <see langword="null"/> when it has none.
    /// </summary>
    public string? Key { get; init; }
}
