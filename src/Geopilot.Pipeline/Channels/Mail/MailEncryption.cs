namespace Geopilot.Pipeline.Channels.Mail;

/// <summary>
/// How the mail channel secures its connection to the mail server.
/// </summary>
internal enum MailEncryption
{
    /// <summary>
    /// Connects in plain text and switches to TLS before anything else is sent (STARTTLS), usually on port 587. A server
    /// that does not offer it fails the attempt.
    /// </summary>
    StartTls,

    /// <summary>
    /// Connects with TLS right away, usually on port 465.
    /// </summary>
    SslOnConnect,

    /// <summary>
    /// Sends unencrypted, only without a login, such as to a relay in the own network.
    /// </summary>
    None,
}
