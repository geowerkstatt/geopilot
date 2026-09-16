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
    /// Name of the unique index over <see cref="Mandate.Key"/>. A violation of this index is how the API
    /// learns that a key is already taken, so the name is shared instead of repeated as a literal.
    /// </summary>
    public const string MandateKeyIndexName = "IX_Mandates_Key";

    /// <summary>
    /// Name of the unique index over <see cref="Delivery.JobId"/>. A violation of this index is how the
    /// declaration learns that another caller declared the same job first, so the name is shared instead of
    /// repeated as a literal.
    /// </summary>
    public const string DeliveryJobIndexName = "IX_Deliveries_JobId";

    /// <summary>
    /// Name of the unique index over <see cref="MachineClient.AuthIdentifier"/>. A violation of this index is
    /// how the administration learns that an identifier is already registered, so the name is shared instead
    /// of repeated as a literal.
    /// </summary>
    public const string MachineClientIdentifierIndexName = "IX_MachineClients_AuthIdentifier";

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
    /// Set of all <see cref="MachineClient"/>.
    /// </summary>
    public DbSet<MachineClient> MachineClients { get; set; }

    /// <summary>
    /// Gets the <see cref="MachineClient"/> entity with all includes.
    /// </summary>
    public IQueryable<MachineClient> MachineClientsWithIncludes
    {
        get
        {
            return MachineClients
                .Include(c => c.Organisations)
                .Include(c => c.Deliveries);
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
                .Include(o => o.MachineClients)
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
                .Include(d => d.DeclaringClient)
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
                .ThenInclude(d => d.DeclaringClient)
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
    /// Set of all <see cref="PipelineRunFile"/> records.
    /// </summary>
    public DbSet<PipelineRunFile> PipelineRunFiles { get; set; }

    /// <summary>
    /// Set of all <see cref="PipelineRunCondition"/> records.
    /// </summary>
    public DbSet<PipelineRunCondition> PipelineRunConditions { get; set; }

    /// <summary>
    /// Set of all <see cref="PipelineRunArtifact"/> records.
    /// </summary>
    public DbSet<PipelineRunArtifact> PipelineRunArtifacts { get; set; }

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

        // A job yields at most one delivery. The service checks that before it writes, but the check and the
        // write are two statements, so only the database can keep two callers from passing it at the same time
        // and recording the assets of one job twice. Filtered on the soft delete, so a deleted delivery does
        // not keep its job from being delivered again, which is what the check does as well.
        modelBuilder.Entity<Delivery>()
            .HasIndex(delivery => delivery.JobId)
            .IsUnique()
            .HasFilter("\"Deleted\" = false")
            .HasDatabaseName(DeliveryJobIndexName);

        // A delivery is declared by a user or by a machine client, never by both and never by neither. The
        // navigations are optional so either can be absent, and the database keeps exactly one of them set.
        // Restrict on both: users and clients are only ever deactivated, and a delivery must not lose who
        // declared it.
        modelBuilder.Entity<Delivery>(delivery =>
        {
            delivery.HasOne(d => d.DeclaringUser).WithMany(u => u.Deliveries).HasForeignKey(d => d.DeclaringUserId).OnDelete(DeleteBehavior.Restrict);
            delivery.HasOne(d => d.DeclaringClient).WithMany(c => c.Deliveries).HasForeignKey(d => d.DeclaringClientId).OnDelete(DeleteBehavior.Restrict);
            delivery.ToTable(t => t.HasCheckConstraint("CK_Deliveries_Declarer", "(\"DeclaringUserId\" IS NULL) <> (\"DeclaringClientId\" IS NULL)"));
        });

        modelBuilder.Entity<MachineClient>()
            .HasIndex(client => client.AuthIdentifier)
            .IsUnique()
            .HasDatabaseName(MachineClientIdentifierIndexName);

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
            .HasIndex(mandate => mandate.Key)
            .IsUnique()
            .HasDatabaseName(MandateKeyIndexName);

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
            run.HasOne(r => r.MachineClient).WithMany().HasForeignKey(r => r.MachineClientId).OnDelete(DeleteBehavior.Restrict);

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

        modelBuilder.Entity<User>()
            .HasIndex(u => u.AuthIdentifier)
            .IsUnique();
    }
}
