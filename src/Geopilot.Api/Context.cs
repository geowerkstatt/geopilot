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

    /// <summary>
    /// Set of all <see cref="PipelineRun"/> execution protocol records.
    /// </summary>
    public DbSet<PipelineRun> PipelineRuns { get; set; }

    /// <summary>
    /// Set of all <see cref="PipelineRunStep"/> records. Exposed so the per-step upsert can address a
    /// row by run and step id without loading the whole run.
    /// </summary>
    public DbSet<PipelineRunStep> PipelineRunSteps { get; set; }

    /// <summary>
    /// Gets the <see cref="PipelineRun"/> entity with all includes.
    /// </summary>
    public IQueryable<PipelineRun> PipelineRunsWithIncludes
    {
        get
        {
            return PipelineRuns
                .Include(r => r.Files)
                .Include(r => r.Steps)
                .ThenInclude(s => s.Conditions)
                .Include(r => r.Steps)
                .ThenInclude(s => s.Artifacts);
        }
    }

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

        // The execution protocol is append-only: no Deleted flags, no query filters, and enums stored as
        // text so the tables stay readable in SQL and reordering an enum cannot reinterpret history.
        modelBuilder.Entity<PipelineRun>(run =>
        {
            run.HasIndex(r => r.JobId).IsUnique();
            run.Property(r => r.Definition).HasColumnType("jsonb");
            run.Property(r => r.ClientKind).HasConversion<string>();
            run.Property(r => r.ScanState).HasConversion<string>();
            run.Property(r => r.TerminalState).HasConversion<string>();

            // Restrict instead of the optional-FK default (set null): mandates and users are only ever
            // soft-deleted, and the protocol must not silently lose who a run belonged to.
            run.HasOne(r => r.Mandate).WithMany().HasForeignKey(r => r.MandateId).OnDelete(DeleteBehavior.Restrict);
            run.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);

            run.HasMany(r => r.Files).WithOne(f => f.PipelineRun).HasForeignKey(f => f.PipelineRunId).OnDelete(DeleteBehavior.Cascade);
            run.HasMany(r => r.Steps).WithOne(s => s.PipelineRun).HasForeignKey(s => s.PipelineRunId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PipelineRunStep>(step =>
        {
            step.HasIndex(s => new { s.PipelineRunId, s.StepId }).IsUnique();
            step.Property(s => s.State).HasConversion<string>();
            step.Property(s => s.DisplayName).HasColumnType("jsonb").HasConversion(localizedTextConverter, localizedTextComparer);

            // Cast to the non-generic overload: the generic one insists on a ValueConverter<LocalizedText?, ...>
            // for the nullable properties, while EF never passes null through a converter anyway.
            step.Property(s => s.StatusMessage).HasColumnType("jsonb").HasConversion((ValueConverter)localizedTextConverter, localizedTextComparer);
            step.Property(s => s.ConditionMessage).HasColumnType("jsonb").HasConversion((ValueConverter)localizedTextConverter, localizedTextComparer);
            step.HasMany(s => s.Conditions).WithOne(c => c.Step).HasForeignKey(c => c.PipelineRunStepId).OnDelete(DeleteBehavior.Cascade);
            step.HasMany(s => s.Artifacts).WithOne(a => a.Step).HasForeignKey(a => a.PipelineRunStepId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PipelineRunCondition>(condition =>
        {
            condition.Property(c => c.Phase).HasConversion<string>();
            condition.Property(c => c.Kind).HasConversion<string>();
            condition.Property(c => c.EvaluatedValues).HasColumnType("jsonb");
        });

        modelBuilder.Entity<PipelineRunArtifact>(artifact => artifact.Property(a => a.Kind).HasConversion<string>());
    }
}
