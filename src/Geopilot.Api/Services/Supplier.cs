namespace Geopilot.Api.Services;

/// <summary>
/// Whoever delivers: a user or a machine client, identified by id only. Ids rather than entities,
/// because the value travels with a <see cref="Processing.DeliveryIntent"/> into the background, where an entity
/// loaded during the request would belong to a context that is long gone.
/// </summary>
public sealed record Supplier
{
    private Supplier(int? userId, int? machineClientId)
    {
        UserId = userId;
        MachineClientId = machineClientId;
    }

    /// <summary>
    /// The id of the delivering user, or <see langword="null"/> when a machine client delivers.
    /// </summary>
    public int? UserId { get; }

    /// <summary>
    /// The id of the delivering machine client, or <see langword="null"/> when a user delivers.
    /// </summary>
    public int? MachineClientId { get; }

    /// <summary>
    /// Whether a machine client delivers.
    /// </summary>
    public bool IsClient => MachineClientId is not null;

    /// <summary>
    /// The supplier that is the user with the specified id.
    /// </summary>
    public static Supplier ForUser(int userId) => new(userId, null);

    /// <summary>
    /// The supplier that is the machine client with the specified id.
    /// </summary>
    public static Supplier ForClient(int machineClientId) => new(null, machineClientId);

    /// <inheritdoc/>
    public override string ToString() => IsClient ? $"machine client {MachineClientId}" : $"user {UserId}";
}
