using Geopilot.Api.Models;

namespace Geopilot.Api.DTOs;

/// <summary>
/// A company or group of users that may have a mandate for delivering data to the system owner.
/// </summary>
public class OrganisationDto
{
    /// <summary>
    /// Create a new <see cref="OrganisationDto"/> from a <see cref="Organisation"/>.
    /// </summary>
    public static OrganisationDto FromOrganisation(Organisation organisation)
    {
        return new OrganisationDto
        {
            Id = organisation.Id,
            Name = organisation.Name,
            Users = organisation.Users.Select(u => u.Id).ToList(),
            Mandates = organisation.Mandates.Select(m => m.Id).ToList(),
        };
    }

    /// <summary>
    /// The unique identifier for the organisation.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The display name of the organisation.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// IDs of the users that are members of the organisation.
    /// </summary>
    public List<int> Users { get; set; } = new List<int>();

    /// <summary>
    /// IDs of the mandates the organisation has for delivering data to the system owner.
    /// </summary>
    public List<int> Mandates { get; set; } = new List<int>();
}
