using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api.Models
{
    internal class Organisation
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<User> Users { get; set; } = new List<User>();

        public List<Operat> Operate { get; set; } = new List<Operat>();
    }
}
