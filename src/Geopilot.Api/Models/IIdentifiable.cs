namespace Geopilot.Api.Models;

/// <summary>
/// An object that can be identified by a numeric ID.
/// </summary>
public interface IIdentifiable
{
    /// <summary>
    /// Gets or sets the entity's id.
    /// </summary>
    public int Id { get; set; }
}
