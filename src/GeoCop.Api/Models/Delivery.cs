using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GeoCop.Api.Models;

/// <summary>
/// A fullfillment of a <see cref="DeliveryMandate"/>. Contains all relevant meta information and assets provided or created by the validation and delivery process.
/// </summary>
public class Delivery
{
    /// <summary>
    /// The unique identifier for the delivery.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The date the delivery was declared.
    /// </summary>
    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The user that declared the delivery.
    /// </summary>
    [JsonIgnore]
    public User DeclaringUser { get; set; } = new User();

    /// <summary>
    /// The mandate the delivery fulfills.
    /// </summary>
    [JsonIgnore]
    public DeliveryMandate DeliveryMandate { get; set; } = new DeliveryMandate();

    /// <summary>
    /// Assets delivered or created by the validation and delivery process.
    /// </summary>
    [JsonIgnore]
    public List<Asset> Assets { get; set; } = new List<Asset>();
}
