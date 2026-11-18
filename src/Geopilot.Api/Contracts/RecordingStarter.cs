namespace Geopilot.Api.Contracts;

/// <summary>
/// Who started the recorded job: a user or a machine client, told apart by which id is set. A job started without
/// signing in has none.
/// </summary>
/// <param name="UserId">The id of the user, or <see langword="null"/> when a machine client started the run.</param>
/// <param name="MachineClientId">The id of the machine client, or <see langword="null"/> when a user started the run.</param>
/// <param name="Name">The display name of the user or the machine client.</param>
public record RecordingStarter(int? UserId, int? MachineClientId, string Name);
