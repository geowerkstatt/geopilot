using Geopilot.Api.Models;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

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
    /// Gets the <see cref="User"/> entity with all includes.
    /// </summary>
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
                .ThenInclude(d => d.DeclaringUser)
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
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Delivery>().HasQueryFilter(d => !d.Deleted);
        modelBuilder.Entity<Asset>()
            .HasQueryFilter(a => !a.Delivery.Deleted)
            .HasQueryFilter(a => !a.Deleted);

        var localizedTextConverter = new ValueConverter<LocalizedText, string>(
            localizedText => JsonSerializer.Serialize(localizedText, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<LocalizedText>(json, (JsonSerializerOptions?)null) ?? LocalizedText.Empty);

        var localizedTextComparer = new ValueComparer<LocalizedText>(
            (left, right) => object.Equals(left, right),
            localizedText => localizedText.GetHashCode(),
            localizedText => localizedText);

        modelBuilder.Entity<Mandate>()
            .Property(mandate => mandate.Name)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("jsonb_build_object()")
            .HasConversion(localizedTextConverter, localizedTextComparer);

        modelBuilder.Entity<Mandate>()
            .Property(mandate => mandate.Description)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("jsonb_build_object()")
            .HasConversion(localizedTextConverter, localizedTextComparer);
    }
}
