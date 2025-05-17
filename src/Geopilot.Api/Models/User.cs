using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Geopilot.Api.Models;

public enum UserType
{
    HUMAN,
    MACHINE
}

/// <summary>
/// A person that is allowed to view or declare deliveries.
/// </summary>
public class User
{
    /// <summary>
    /// The unique identifier for the user.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The unique identifier for the user in the authentication system.
    /// </summary>
    [JsonIgnore]
    public string AuthIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// The unique login identifier or username
    /// </summary>
    public string LoginIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// The email address of the user.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// The full name of the user.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the user is an administrator.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Account type for user "Human/Machine".
    /// </summary>
    public UserType UserType { get; set; }

    /// <summary>
    /// Organisations the user is a member of.
    /// </summary>
    public List<Organisation> Organisations { get; set; } = new List<Organisation>();

    /// <summary>
    /// Deliveries the user has declared.
    /// </summary>
    public List<Delivery> Deliveries { get; set; } = new List<Delivery>();
}
