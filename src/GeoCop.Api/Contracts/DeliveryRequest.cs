using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api.Contracts
{
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
    }
}
