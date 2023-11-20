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
        /// <param name="options">Configuration options for the Context.</param>
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
        /// Gets the <see cref="Delivery"/> entity with all includes.
        /// </summary>
        public IQueryable<Delivery> DeliveriesWithIncludes
        {
            get
            {
                return Deliveries
                    .Include(d => d.DeliveryMandate)
                    .Include(d => d.Assets)
                    .AsNoTracking();
            }
        }

        /// <summary>
        /// Set of all <see cref="DeliveryMandate"/>.
        /// </summary>
        public DbSet<DeliveryMandate> DeliveryMandates { get; set; }

        /// <summary>
        /// Gets the <see cref="DeliveryMandate"/> entity with all includes.
        /// </summary>
        public IQueryable<DeliveryMandate> DeliveryMandatesWithIncludes
        {
            get
            {
                return DeliveryMandates
                    .Include(d => d.Deliveries)
                    .ThenInclude(d => d.Assets)
                    .AsNoTracking();
            }
        }

        /// <summary>
        /// Set of all <see cref="Asset"/>.
        /// </summary>
        public DbSet<Asset> Assets { get; set; }
    }
}
