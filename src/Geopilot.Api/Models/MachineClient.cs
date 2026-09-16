using System.ComponentModel.DataAnnotations.Schema;

namespace Geopilot.Api.Models;

/// <summary>
/// A machine that delivers data on behalf of an organisation, authenticated with client credentials at the
/// identity provider of the installation. Deliberately not a <see cref="User"/>: a client has no account in
/// the web interface, and keeping it out of that type is what keeps its credentials out of the interface.
/// A client is registered by an administrator; nothing creates one from a token.
/// </summary>
public class MachineClient
{
    /// <summary>
    /// The unique identifier for the client.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The identifier the identity provider puts into the token (the <c>sub</c> claim, or what the
    /// introspection reports as the subject). An administrator copies it from the identity provider or from
    /// the log line of a refused attempt; this is how a token is matched to its registration.
    /// </summary>
    public string AuthIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// The display name, chosen by the administrator. It is what a delivery of this client names as the
    /// deliverer, since the token carries no name of its own.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The current state of the client.
    /// </summary>
    [Column(TypeName = "varchar(24)")]
    public MachineClientState State { get; set; } = MachineClientState.Active;

    /// <summary>
    /// Organisations the client delivers for, and thereby the mandates it may deliver to.
    /// </summary>
    public List<Organisation> Organisations { get; set; } = new List<Organisation>();

    /// <summary>
    /// Deliveries the client has declared.
    /// </summary>
    public List<Delivery> Deliveries { get; set; } = new List<Delivery>();
}
