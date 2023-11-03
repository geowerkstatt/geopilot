using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api.Models
{
    internal class Delivery
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public DateTime Date { get; set; } = DateTime.Now;

        public User DeclaringUser { get; set; } = new User();

        public Operat Operat { get; set; } = new Operat();

        public List<Asset> Assets { get; set; } = new List<Asset>();
    }
}
