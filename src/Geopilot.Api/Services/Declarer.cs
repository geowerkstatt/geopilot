namespace Geopilot.Api.Services;

/// <summary>
/// Whoever declares a delivery: a user or a machine client, identified by id only. Ids rather than entities,
/// because the value travels with a <see cref="Processing.Submission"/> into the background, where an entity
/// loaded during the request would belong to a context that is long gone.
/// </summary>
public sealed record Declarer
{
    private Declarer(int? userId, int? machineClientId)
    {
        UserId = userId;
        MachineClientId = machineClientId;
    }

    /// <summary>
    /// The id of the declaring user, or <see langword="null"/> when a machine client declares.
    /// </summary>
    public int? UserId { get; }

    /// <summary>
    /// The id of the declaring machine client, or <see langword="null"/> when a user declares.
    /// </summary>
    public int? MachineClientId { get; }

    /// <summary>
    /// Whether a machine client declares.
    /// </summary>
    public bool IsClient => MachineClientId is not null;

    /// <summary>
    /// The declarer that is the user with the specified id.
    /// </summary>
    public static Declarer ForUser(int userId) => new(userId, null);

    /// <summary>
    /// The declarer that is the machine client with the specified id.
    /// </summary>
    public static Declarer ForClient(int machineClientId) => new(null, machineClientId);

    /// <inheritdoc/>
    public override string ToString() => IsClient ? $"machine client {MachineClientId}" : $"user {UserId}";
}
