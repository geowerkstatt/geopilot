namespace GeoCop.Api.Models
{
    /// <summary>
    /// A person allowed log in to the system and view or declare deliveries.
    /// </summary>
    public class User
    {
        /// <summary>
        /// The unique identifier for the user.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The unique identifier for the user in the authentication system.
        /// </summary>
        public string AuthIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Organisations the user is a member of.
        /// </summary>
        public List<Organisation> Organisations { get; set; } = new List<Organisation>();

        /// <summary>
        /// Deliveries the user has declared.
        /// </summary>
        public List<Delivery> Deliveries { get; set; } = new List<Delivery>();
    }
}
