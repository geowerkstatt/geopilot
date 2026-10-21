namespace Geopilot.Api.Models;

/// <summary>
/// Who may see the deliveries of a <see cref="Mandate"/> in the STAC catalog and download their assets.
/// </summary>
public enum StacAccess
{
    /// <summary>
    /// Only administrators.
    /// </summary>
    AdminsOnly,

    /// <summary>
    /// Anyone, without signing in.
    /// </summary>
    Public,
}
