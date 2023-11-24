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

/// <summary>
/// A delivery DTO of a <see cref="Delivery"/>. Contains all relevant meta information.
/// </summary>
/// <param name="id">Id of the delivery</param>
/// <param name="date">Upload date of the delivery</param>
/// <param name="declaringUser">Uploading user</param>
/// <param name="deliveryMandate">Mandate of the delivery</param>
public record DeliveryDto(int id, DateTime date, string declaringUser, string deliveryMandate);
