using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api.Contracts;

/// <summary>
/// Request for transforming a validation to a delivery.
/// </summary>
public class DeliveryRequest
{
    /// <summary>
    /// The job identification provided by the validation endpoint.
    /// </summary>
    [Required]
    public Guid JobId { get; set; }

    /// <summary>
    /// The id of a DeliveryMandate selected by the user to reference.
    /// </summary>
    [Required]
    public int DeliveryMandateId { get; set; }

    /// <summary>
    /// Indicate whether the delivery contains partial data.
    /// </summary>
    [Required]
    public bool PartialDelivery { get; set; }

    /// <summary>
    /// Optional. The id of a previous delivery on the same DeliveryMandate.
    /// </summary>
    public int? PrecursorDeliveryId { get; set; }

    /// <summary>
    /// Optional. Comment to describe the delivery.
    /// </summary>
    public string? Comment { get; set; }
}
