using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api.Models
{
    /// <summary>
    /// A person allowed log in to the system and view or declare deliveries.
    /// </summary>
    internal class User
    {
        public int Id { get; set; }

        public string AuthIdentifier { get; set; } = string.Empty;

        public List<Organisation> Organisations { get; set; } = new List<Organisation>();

        public List<Delivery> Deliveries { get; set; } = new List<Delivery>();
    }
}
