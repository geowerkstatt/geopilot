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
        /// Initializes a new instance of the <see cref="Context"/> class.
        /// </summary>
        /// <param name="options"></param>
        public Context(DbContextOptions<Context> options)
            : base(options)
        {
        }

        /// <summary>
        /// The entity set for <see cref="Users"/> objects.
        /// </summary>
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// The entity set for <see cref="Organisations"/> objects.
        /// </summary>
        public DbSet<Organisation> Organisations { get; set; }

        /// <summary>
        /// The entity set for <see cref="Delivery"/> objects.
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
        /// The entity set for <see cref="DeliveryMandate"/> objects.
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
        /// The entity set for <see cref="Asset"/> objects.
        /// </summary>
        public DbSet<Asset> Assets { get; set; }
    }
}
