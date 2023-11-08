using GeoCop.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GeoCop.Api
{
    /// <summary>
    /// Database context to manage the delivery database.
    /// </summary>
    public class Context : DbContext
    {
        public Context(DbContextOptions<Context> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        public DbSet<Organisation> Organisations { get; set; }

        public DbSet<Delivery> Deliveries { get; set; }

        public DbSet<DeliveryMandate> DeliveryMandates { get; set; }

        public DbSet<Asset> Assets { get; set; }
    }
}
