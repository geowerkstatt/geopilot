using GeoCop.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GeoCop.Api
{
    /// <summary>
    /// Database context to manage the delivery database.
    /// </summary>
    public class Context : DbContext
    {
        /// <summary>
        /// Database context to manage the database.
        /// </summary>
        /// <param name="options"></param>
        public Context(DbContextOptions<Context> options)
            : base(options)
        {
        }

        /// <summary>
        /// Set of all <see cref="User"/>.
        /// </summary>
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// Set of all <see cref="Organisation"/>.
        /// </summary>
        public DbSet<Organisation> Organisations { get; set; }

        /// <summary>
        /// Set of all <see cref="DeliveryMandate"/>.
        /// </summary>
        public DbSet<Delivery> Deliveries { get; set; }

        /// <summary>
        /// Set of all <see cref="DeliveryMandate"/>.
        /// </summary>
        public DbSet<DeliveryMandate> DeliveryMandates { get; set; }

        /// <summary>
        /// Set of all <see cref="Asset"/>.
        /// </summary>
        public DbSet<Asset> Assets { get; set; }
    }
}
