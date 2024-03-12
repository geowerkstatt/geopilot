using Geopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Geopilot.Api;

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
    public List<Delivery> DeliveriesWithIncludes
    {
        get
        {
            return Deliveries
                .Where(d => !d.Deleted)
                .Include(d => d.DeliveryMandate)
                .Include(d => d.Assets)
                .Include(d => d.DeclaringUser)
                .AsNoTracking()
                .ToList();
        }
    }

    /// <summary>
    /// Set of all <see cref="DeliveryMandate"/>.
    /// </summary>
    public DbSet<DeliveryMandate> DeliveryMandates { get; set; }

    /// <summary>
    /// Gets the <see cref="DeliveryMandate"/> entity with all includes.
    /// </summary>
    public List<DeliveryMandate> DeliveryMandatesWithIncludes
    {
        get
        {
            return DeliveryMandates
                .Include(d => d.Deliveries.Where(delivery => !delivery.Deleted))
                .ThenInclude(d => d.Assets)
                .AsNoTracking()
                .ToList();
        }
    }

    /// <summary>
    /// Set of all <see cref="Asset"/>.
    /// </summary>
    public DbSet<Asset> Assets { get; set; }
}
