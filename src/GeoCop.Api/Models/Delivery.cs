using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api.Models
{
    /// <summary>
    /// A fullfillment of a <see cref="DeliveryMandate"/>. Contains all relevant meta infromation and assets provided or created by the validation and delivery process.
    /// </summary>
    internal class Delivery
    {
        public int Id { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        public User DeclaringUser { get; set; } = new User();

        public DeliveryMandate DeliveryMandate { get; set; } = new DeliveryMandate();

        public List<Asset> Assets { get; set; } = new List<Asset>();
    }
}
