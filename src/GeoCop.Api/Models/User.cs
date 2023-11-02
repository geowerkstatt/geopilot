using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api.Models
{
    internal class User
    {
        [Key]
        public string Identifier { get; set; } = string.Empty;

        public List<Organisation> Organisations { get; set; } = new List<Organisation>();

        public List<Delivery> Deliveries { get; set; } = new List<Delivery>();
    }
}
