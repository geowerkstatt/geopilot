using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api.Models
{
    /// <summary>
    /// A fullfillment of a <see cref="DeliveryMandate"/>. Contains all relevant meta infromation and assets provided or created by the validation and delivery process.
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
        public DateTime Date { get; set; } = DateTime.Now;

        /// <summary>
        /// The user that declared the delivery.
        /// </summary>
        public User DeclaringUser { get; set; } = new User();

        /// <summary>
        /// The mandate the delivery fulfills.
        /// </summary>
        public DeliveryMandate DeliveryMandate { get; set; } = new DeliveryMandate();

        /// <summary>
        /// Assets delivered or created by the validation and delivery process.
        /// </summary>
        public List<Asset> Assets { get; set; } = new List<Asset>();
    }
}
