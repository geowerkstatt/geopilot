namespace Geopilot.Api.Models;

/// <summary>
/// A company or group of users that may have a mandate for delivering data to the system owner.
/// </summary>
public class Organisation
{
    /// <summary>
    /// The unique identifier for the organisation.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The display name of the organisation.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Users that are members of the organisation.
    /// </summary>
    public List<User> Users { get; set; } = new List<User>();

    /// <summary>
    /// Mandates the organisation has for delivering data to the system owner.
    /// </summary>
    public List<Mandate> Mandates { get; set; } = new List<Mandate>();
}
