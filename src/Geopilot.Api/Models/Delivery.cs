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
    /// The user that declared the delivery.
    /// </summary>
    public User DeclaringUser { get; set; } = new User();

    /// <summary>
    /// The mandate the delivery fulfills.
    /// </summary>
    public Mandate Mandate { get; set; } = new Mandate();

    /// <summary>
    /// Assets delivered or created by the validation and delivery process.
    /// </summary>
    public List<Asset> Assets { get; set; } = new List<Asset>();

    /// <summary>
    /// Indicate whether the delivery contains partial data.
    /// </summary>
    public bool Partial { get; set; }

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
}
