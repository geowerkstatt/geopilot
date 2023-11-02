using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api.Models
{
    internal class Organisation
    {
        [Key]
        public string Name { get; set; } = string.Empty;

        public List<User> Users { get; set; } = new List<User>();
    }
}
