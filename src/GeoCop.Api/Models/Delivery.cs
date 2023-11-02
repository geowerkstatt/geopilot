using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api.Models
{
    internal class Delivery
    {
        [Key]
        public string FileHash { get; set; } = string.Empty;

        public string Filename { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.Now;

        public User DeclaringUser { get; set; } = new User();

        public Operat Operat { get; set; } = new Operat();

    }
}
