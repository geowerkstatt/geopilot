using Geopilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

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

    public IQueryable<User> UsersWithIncludes
    {
        get
        {
            return Users
                .Include(u => u.Organisations)
                .Include(u => u.Deliveries);
        }
    }

    /// <summary>
    /// Set of all <see cref="Organisation"/>.
    /// </summary>
    public DbSet<Organisation> Organisations { get; set; }

    /// <summary>
    /// Gets the <see cref="Organisation"/> entity with all includes.
    /// </summary>
    public IQueryable<Organisation> OrganisationsWithIncludes
    {
        get
        {
            return Organisations
                .Include(o => o.Users)
                .Include(o => o.Mandates);
        }
    }

    /// <summary>
    /// Set of all <see cref="Delivery"/>.
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
                .Include(d => d.Mandate)
                .Include(d => d.Assets)
                .Include(d => d.DeclaringUser)
                .Include(d => d.PrecursorDelivery);
        }
    }

    /// <summary>
    /// Set of all <see cref="Mandate"/>.
    /// </summary>
    public DbSet<Mandate> Mandates { get; set; }

    /// <summary>
    /// Gets the <see cref="Mandate"/> entity with all includes.
    /// </summary>
    public IQueryable<Mandate> MandatesWithIncludes
    {
        get
        {
            return Mandates
                .Include(m => m.Organisations)
                .ThenInclude(o => o.Users)
                .Include(m => m.Deliveries)
                .ThenInclude(d => d.Assets);
        }
    }

    /// <summary>
    /// Set of all <see cref="Asset"/>.
    /// </summary>
    public DbSet<Asset> Assets { get; set; }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Delivery>().HasQueryFilter(d => !d.Deleted);
    }
}
