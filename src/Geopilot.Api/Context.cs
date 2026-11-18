using Geopilot.Api.Models;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    /// creation learns that another caller created a delivery for the same job first, so the name is shared instead of
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
                .Include(u => u.Deliveries)
                .Include(u => u.ResponsibleForMachineClients);
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
                .Include(c => c.Deliveries)
                .Include(c => c.ResponsibleUsers);
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
                .Include(d => d.DeliveredByUser)
                .Include(d => d.DeliveredByClient)
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
                .ThenInclude(d => d.DeliveredByUser)
                .Include(m => m.Deliveries)
                .ThenInclude(d => d.DeliveredByClient)
                .Include(m => m.Deliveries)
                .ThenInclude(d => d.Assets);
        }
    }

    /// <summary>
    /// Set of all <see cref="Asset"/>.
    /// </summary>
    public DbSet<Asset> Assets { get; set; }

    /// <summary>
    /// Set of all <see cref="DeliveryReaction"/>, the reactions to deliveries and the queue they are claimed from.
    /// </summary>
    public DbSet<DeliveryReaction> DeliveryReactions { get; set; }

    /// <summary>
    /// Set of all <see cref="Recording"/> execution protocol records.
    /// </summary>
    public DbSet<Recording> Recordings { get; set; }

    /// <summary>
    /// Set of all <see cref="RecordingStep"/> records. Exposed so the per-step upsert can address a
    /// row by run and step id without loading the whole run.
    /// </summary>
    public DbSet<RecordingStep> RecordingSteps { get; set; }

    /// <summary>
    /// Set of all <see cref="RecordingFile"/> records.
    /// </summary>
    public DbSet<RecordingFile> RecordingFiles { get; set; }

    /// <summary>
    /// Set of all <see cref="RecordingInput"/> records.
    /// </summary>
    public DbSet<RecordingInput> RecordingInputs { get; set; }

    /// <summary>
    /// Set of all <see cref="RecordingCondition"/> records.
    /// </summary>
    public DbSet<RecordingCondition> RecordingConditions { get; set; }

    /// <summary>
    /// Set of all <see cref="RecordingArtifact"/> records.
    /// </summary>
    public DbSet<RecordingArtifact> RecordingArtifacts { get; set; }

    /// <summary>
    /// Set of all <see cref="RecordingReactionEvent"/> records.
    /// </summary>
    public DbSet<RecordingReactionEvent> RecordingReactionEvents { get; set; }

    /// <summary>
    /// Gets the <see cref="Recording"/> entity with all includes.
    /// </summary>
    public IQueryable<Recording> RecordingsWithIncludes
    {
        get
        {
            return Recordings
                .Include(r => r.Files)
                .Include(r => r.Steps)
                .ThenInclude(s => s.Inputs)
                .Include(r => r.Steps)
                .ThenInclude(s => s.Conditions)
                .Include(r => r.Steps)
                .ThenInclude(s => s.Artifacts)
                .Include(r => r.ReactionEvents);
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

        // A delivery is made by a user or by a machine client, never by both and never by neither. The
        // navigations are optional so either can be absent, and the database keeps exactly one of them set.
        // Restrict on both: users and clients are only ever deactivated, and a delivery must not lose who
        // delivered it.
        modelBuilder.Entity<Delivery>(delivery =>
        {
            delivery.HasOne(d => d.DeliveredByUser).WithMany(u => u.Deliveries).HasForeignKey(d => d.DeliveredByUserId).OnDelete(DeleteBehavior.Restrict);
            delivery.HasOne(d => d.DeliveredByClient).WithMany(c => c.Deliveries).HasForeignKey(d => d.DeliveredByClientId).OnDelete(DeleteBehavior.Restrict);
            delivery.ToTable(t => t.HasCheckConstraint("CK_Deliveries_DeliveredBy", "(\"DeliveredByUserId\" IS NULL) <> (\"DeliveredByClientId\" IS NULL)"));
        });

        modelBuilder.Entity<MachineClient>()
            .HasIndex(client => client.AuthIdentifier)
            .IsUnique()
            .HasDatabaseName(MachineClientIdentifierIndexName);

        // The columns are named like those of the other join tables, not after the navigations.
        modelBuilder.Entity<MachineClient>()
            .HasMany(client => client.ResponsibleUsers)
            .WithMany(user => user.ResponsibleForMachineClients)
            .UsingEntity(
                "MachineClientUser",
                right => right.HasOne(typeof(User)).WithMany().HasForeignKey("UsersId"),
                left => left.HasOne(typeof(MachineClient)).WithMany().HasForeignKey("MachineClientsId"));

        modelBuilder.Entity<Asset>()
            .HasQueryFilter(a => !a.Delivery.Deleted)
            .HasQueryFilter(a => !a.Deleted);

        // The snapshot holds dictionaries, so the comparer compares the JSON, as for the reaction set values of a mandate.
        var reactionSnapshotConverter = new ValueConverter<ReactionSnapshot, string>(
            snapshot => JsonSerializer.Serialize(snapshot, (JsonSerializerOptions?)null),
            json => ReactionSnapshot.FromJson(json));

        var reactionSnapshotComparer = new ValueComparer<ReactionSnapshot>(
            (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
            snapshot => JsonSerializer.Serialize(snapshot, (JsonSerializerOptions?)null).GetHashCode(StringComparison.Ordinal),
            snapshot => ReactionSnapshot.FromJson(JsonSerializer.Serialize(snapshot, (JsonSerializerOptions?)null)));

        var localizedTextConverter = new ValueConverter<LocalizedText, string>(
            localizedText => JsonSerializer.Serialize(localizedText, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<LocalizedText>(json, (JsonSerializerOptions?)null) ?? LocalizedText.Empty);

        var localizedTextComparer = new ValueComparer<LocalizedText>(
            (left, right) => object.Equals(left, right),
            localizedText => localizedText.GetHashCode(),
            localizedText => localizedText);

        modelBuilder.Entity<DeliveryReaction>(reaction =>
        {
            // The reactions of a deleted delivery are never claimed, listed or reset again.
            reaction.HasQueryFilter(r => !r.Delivery.Deleted);
            reaction.HasOne(r => r.Delivery).WithMany().HasForeignKey(r => r.DeliveryId);
            reaction.HasIndex(r => new { r.DeliveryId, r.ReactionId }).IsUnique();

            // The queue looks for open reactions in the order they fall due.
            reaction.HasIndex(r => r.NextAttemptAt).HasFilter("\"State\" IN ('Pending', 'Running')");

            reaction.Property(r => r.ReactionId).HasMaxLength(128);
            reaction.Property(r => r.Kind).HasConversion<string>().HasMaxLength(12);
            reaction.Property(r => r.State).HasConversion<string>().HasMaxLength(12);
            reaction.Property(r => r.Config).HasColumnType("jsonb").HasConversion(reactionSnapshotConverter, reactionSnapshotComparer);
            reaction.Property(r => r.Message).HasColumnType("jsonb").HasConversion((ValueConverter)localizedTextConverter, localizedTextComparer);
        });

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

        // Null only in a request that leaves the values out, which the controller resolves before saving; a new
        // mandate saved without them gets the default.
        var reactionSetValuesConverter = new ValueConverter<Dictionary<string, Dictionary<string, JsonElement>>?, string>(
            values => JsonSerializer.Serialize(values, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json, (JsonSerializerOptions?)null));

        var reactionSetValuesComparer = new ValueComparer<Dictionary<string, Dictionary<string, JsonElement>>?>(
            (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
            values => JsonSerializer.Serialize(values, (JsonSerializerOptions?)null).GetHashCode(StringComparison.Ordinal),
            values => JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(JsonSerializer.Serialize(values, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null));

        modelBuilder.Entity<Mandate>()
            .Property(mandate => mandate.ReactionSetValues)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("jsonb_build_object()")
            .HasConversion(reactionSetValuesConverter, reactionSetValuesComparer)
            .IsRequired();

        // The stored JSON has the shape the API writes: camelCase properties and camelCase enum strings.
        var metadataJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        modelBuilder.Entity<Mandate>()
            .Property(mandate => mandate.MetadataFields)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("jsonb_build_array()")
            .HasConversion(MetadataConverter<MetadataField>(metadataJsonOptions), MetadataComparer<MetadataField>(metadataJsonOptions))
            .IsRequired();

        modelBuilder.Entity<Delivery>()
            .Property(delivery => delivery.Metadata)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("jsonb_build_array()")
            .HasConversion(MetadataConverter<DeliveredMetadataField>(metadataJsonOptions), MetadataComparer<DeliveredMetadataField>(metadataJsonOptions))
            .IsRequired();

        // The execution protocol is append-only: no Deleted flags, no query filters, and enums stored as
        // text so the tables stay readable in SQL and reordering an enum cannot reinterpret history.
        modelBuilder.Entity<Recording>(run =>
        {
            run.HasIndex(r => r.JobId).IsUnique();

            // For the list of runs, which pages by start and groups only the runs with a client reference.
            run.HasIndex(r => new { r.StartedAt, r.Id });
            run.HasIndex(r => r.ClientReference).HasFilter("\"ClientReference\" IS NOT NULL");

            run.Property(r => r.Definition).HasColumnType("jsonb");
            run.Property(r => r.ClientKind).HasConversion<string>();
            run.Property(r => r.ClientReference).HasMaxLength(Recording.ClientReferenceMaxLength);
            run.Property(r => r.PermissionToDeliver).HasConversion<string>();
            run.Property(r => r.ScanState).HasConversion<string>();
            run.Property(r => r.TerminalState).HasConversion<string>();

            // Restrict instead of the optional-FK default (set null): mandates and users are only ever
            // soft-deleted, and the protocol must not silently lose who a run belonged to.
            run.HasOne(r => r.Mandate).WithMany().HasForeignKey(r => r.MandateId).OnDelete(DeleteBehavior.Restrict);
            run.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
            run.HasOne(r => r.MachineClient).WithMany().HasForeignKey(r => r.MachineClientId).OnDelete(DeleteBehavior.Restrict);

            // A run is started by a user, by a machine client or anonymously, never by both. The recorder
            // cannot write both (a Supplier is one or the other); the constraint holds that promise for every
            // other writer, the way the one on the delivery does.
            run.ToTable(t => t.HasCheckConstraint("CK_Recordings_StartedBy", "NOT (\"UserId\" IS NOT NULL AND \"MachineClientId\" IS NOT NULL)"));

            run.HasMany(r => r.Files).WithOne(f => f.Recording).HasForeignKey(f => f.RecordingId).OnDelete(DeleteBehavior.Cascade);
            run.HasMany(r => r.Steps).WithOne(s => s.Recording).HasForeignKey(s => s.RecordingId).OnDelete(DeleteBehavior.Cascade);
            run.HasMany(r => r.ReactionEvents).WithOne().HasForeignKey(e => e.RecordingId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecordingReactionEvent>(reactionEvent =>
        {
            reactionEvent.Property(e => e.ReactionId).HasMaxLength(128);
            reactionEvent.Property(e => e.ReactionKind).HasConversion<string>();
            reactionEvent.Property(e => e.Event).HasConversion<string>();
            reactionEvent.Property(e => e.State).HasConversion<string>();
            reactionEvent.Property(e => e.Message).HasColumnType("jsonb").HasConversion((ValueConverter)localizedTextConverter, localizedTextComparer);

            // Restrict like the user of the run: users are only ever soft-deleted.
            reactionEvent.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RecordingStep>(step =>
        {
            step.HasIndex(s => new { s.RecordingId, s.StepId }).IsUnique();
            step.Property(s => s.State).HasConversion<string>();
            step.Property(s => s.DisplayName).HasColumnType("jsonb").HasConversion(localizedTextConverter, localizedTextComparer);

            // Cast to the non-generic overload: the generic one insists on a ValueConverter<LocalizedText?, ...>
            // for the nullable properties, while EF never passes null through a converter anyway.
            step.Property(s => s.StatusMessage).HasColumnType("jsonb").HasConversion((ValueConverter)localizedTextConverter, localizedTextComparer);
            step.Property(s => s.ConditionMessage).HasColumnType("jsonb").HasConversion((ValueConverter)localizedTextConverter, localizedTextComparer);
            step.HasMany(s => s.Inputs).WithOne().HasForeignKey(i => i.RecordingStepId).OnDelete(DeleteBehavior.Cascade);
            step.HasMany(s => s.Conditions).WithOne(c => c.Step).HasForeignKey(c => c.RecordingStepId).OnDelete(DeleteBehavior.Cascade);
            step.HasMany(s => s.Artifacts).WithOne(a => a.Step).HasForeignKey(a => a.RecordingStepId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecordingInput>(input =>
        {
            input.Property(i => i.Origin).HasConversion<string>();
            input.Property(i => i.Kind).HasConversion<string>();
        });

        modelBuilder.Entity<RecordingCondition>(condition =>
        {
            condition.Property(c => c.Phase).HasConversion<string>();
            condition.Property(c => c.Kind).HasConversion<string>();
            condition.Property(c => c.EvaluatedValues).HasColumnType("jsonb");
        });

        modelBuilder.Entity<RecordingArtifact>(artifact => artifact.Property(a => a.Kind).HasConversion<string>());

        modelBuilder.Entity<User>()
            .HasIndex(u => u.AuthIdentifier)
            .IsUnique();
    }

    // A JsonElement has no value equality, so the comparer looks at the serialized JSON like the reaction set values do.
    private static ValueConverter<List<T>, string> MetadataConverter<T>(JsonSerializerOptions options) => new(
        list => JsonSerializer.Serialize(list, options),
        json => JsonSerializer.Deserialize<List<T>>(json, options) ?? new List<T>());

    private static ValueComparer<List<T>> MetadataComparer<T>(JsonSerializerOptions options) => new(
        (left, right) => JsonSerializer.Serialize(left, options) == JsonSerializer.Serialize(right, options),
        list => JsonSerializer.Serialize(list, options).GetHashCode(StringComparison.Ordinal),
        list => JsonSerializer.Deserialize<List<T>>(JsonSerializer.Serialize(list, options), options) ?? new List<T>());
}
