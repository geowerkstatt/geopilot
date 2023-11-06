using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api.Models
{
    /// <summary>
    /// A company or group of users that may have a mandate for delivering data to the system owner.
    /// </summary>
    internal class Organisation
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<User> Users { get; set; } = new List<User>();

        public List<DeliveryMandate> Mandates { get; set; } = new List<DeliveryMandate>();
    }
}
