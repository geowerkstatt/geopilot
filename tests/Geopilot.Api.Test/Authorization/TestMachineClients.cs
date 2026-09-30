using Geopilot.Api.Models;

namespace Geopilot.Api.Authorization;

/// <summary>
/// The machine clients the HTTP tests sign in as. Nothing creates a client from a token, so they have to be
/// registered before a host sees their tokens, and they are committed rather than rolled back because every
/// host reads the database through its own connection. Serialized, because test classes initialize in parallel
/// and the identifier is unique: two hosts racing to register the same client would leave one of them failing
/// on the index.
/// </summary>
internal static class TestMachineClients
{
    private static readonly Lock Gate = new();

    public static void EnsureRegistered(Context context)
    {
        lock (Gate)
        {
            Register(context, JwtTestTokenBuilder.ClientSub, "Active test client", MachineClientState.Active);
            Register(context, JwtTestTokenBuilder.InactiveClientSub, "Inactive test client", MachineClientState.Inactive);
            context.SaveChanges();
        }
    }

    private static void Register(Context context, string sub, string name, MachineClientState state)
    {
        if (context.MachineClients.Any(c => c.AuthIdentifier == sub))
            return;

        context.MachineClients.Add(new MachineClient { AuthIdentifier = sub, Name = name, State = state });
    }
}
