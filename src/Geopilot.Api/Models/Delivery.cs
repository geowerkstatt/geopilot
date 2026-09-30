using System.ComponentModel.DataAnnotations.Schema;

namespace Geopilot.Api.Models;

/// <summary>
/// A fullfillment of a <see cref="Mandate"/>. Contains all relevant meta information and assets provided or created by the validation and delivery process.
/// </summary>
public class Delivery
{
    /// <summary>
    /// The unique identifier for the delivery.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The id of the job with which the delivery was uploaded.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The date the delivery was declared.
    /// </summary>
    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The user that declared the delivery, or <see langword="null"/> when a machine client did.
    /// </summary>
    public User? DeclaringUser { get; set; }

    /// <summary>
    /// The id of <see cref="DeclaringUser"/>. Exposed so a query can filter on it without joining the user.
    /// </summary>
    public int? DeclaringUserId { get; set; }

    /// <summary>
    /// The machine client that declared the delivery, or <see langword="null"/> when a user did. Exactly one
    /// of <see cref="DeclaringUser"/> and <see cref="DeclaringClient"/> is set; the database enforces it.
    /// </summary>
    public MachineClient? DeclaringClient { get; set; }

    /// <summary>
    /// The id of <see cref="DeclaringClient"/>.
    /// </summary>
    public int? DeclaringClientId { get; set; }

    /// <summary>
    /// The name of whoever declared the delivery, for display: the user's full name or the client's name.
    /// </summary>
    [NotMapped]
    public string DeclarerName => DeclaringUser?.FullName ?? DeclaringClient?.Name ?? string.Empty;

    /// <summary>
    /// The mandate the delivery fulfills.
    /// </summary>
    public Mandate Mandate { get; set; } = null!;

    /// <summary>
    /// Assets delivered or created by the validation and delivery process.
    /// </summary>
    public List<Asset> Assets { get; set; } = new List<Asset>();

    /// <summary>
    /// Indicate whether the delivery contains partial data.
    /// </summary>
    public bool? Partial { get; set; }

    /// <summary>
    /// The previous delivery on the same <see cref="Mandate"/>.
    /// </summary>
    public Delivery? PrecursorDelivery { get; set; }

    /// <summary>
    /// Comment to describe the delivery.
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// The deletion status of the delivery.
    /// </summary>
    public bool Deleted { get; set; }

    /// <summary>
    /// Indicates whether the delivery can be deleted by the uploader.
    /// </summary>
    [NotMapped]
    public bool? CanDelete { get; set; }
}
