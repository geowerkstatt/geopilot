namespace Geopilot.Api.Models;

/// <summary>
/// Meta information on how an asset was created and how it has to be interpreted.
/// </summary>
public enum AssetType
{
    /// <summary>
    /// Primary data delivered by the user.
    /// </summary>
    PrimaryData,

    /// <summary>
    /// Data created by the pipeline process.
    /// </summary>
    ProcessedData,

    /// <summary>
    /// Metadata created by the declaration or validation process.
    /// </summary>
    /// <remarks>
    /// This asset type is currently unused.
    /// </remarks>
    Metadata,
}
