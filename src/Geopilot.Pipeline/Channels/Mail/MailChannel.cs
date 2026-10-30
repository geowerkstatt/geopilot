using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace Geopilot.Pipeline.Channels.Mail;

/// <summary>
/// Sends a mail about a forwarded delivery, made from an HTML template, to the recipients the mandate or the pipeline
/// definition names and, with a switch, to the declarer. The base configuration provides the server, the sender and the
/// login. Every attempt sends the same Message-ID, made from the job id of the delivery and the id of the reaction, so a
/// mail server may drop a repetition.
/// </summary>
internal sealed class MailChannel : IDeliveryChannel
{
    private readonly string host;
    private readonly int port;
    private readonly MailboxAddress sender;
    private readonly NetworkCredential? login;
    private readonly MailEncryption encryption;
    private readonly MailTemplate template;
    private readonly IReadOnlyList<MailboxAddress> to;
    private readonly IReadOnlyList<MailboxAddress> cc;
    private readonly IReadOnlyList<MailboxAddress> bcc;
    private readonly bool includeDeclarer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailChannel"/> class.
    /// </summary>
    /// <param name="host">The mail server.</param>
    /// <param name="port">The port of the mail server, usually 587 with STARTTLS and 465 with TLS from the start.</param>
    /// <param name="sender">The address the mail comes from. Its domain also names the channel to the server.</param>
    /// <param name="username">The user to log in with, or <see langword="null"/> to send without a login.</param>
    /// <param name="password">The password of the user.</param>
    /// <param name="encryption">How the connection is secured, STARTTLS when not set.</param>
    /// <param name="template">The HTML of the mail, its title the subject.</param>
    /// <param name="to">The addresses the mail goes to.</param>
    /// <param name="cc">The addresses that get a copy.</param>
    /// <param name="bcc">The addresses that get a copy no other recipient sees.</param>
    /// <param name="includeDeclarer">Whether the declarer goes into <c>To</c> as well, when it is a person with an
    /// address.</param>
    /// <exception cref="ArgumentException">A value is missing or invalid, a login comes without encryption or the
    /// template is refused.</exception>
    public MailChannel(
        [BaseConfigValue] string host,
        [BaseConfigValue] int port,
        [BaseConfigValue] string sender,
        [BaseConfigValue] string? username,
        [BaseConfigValue] string? password,
        [BaseConfigValue] MailEncryption? encryption,
        [ResourceFile] string template,
        [MandateValue] IReadOnlyList<string>? to,
        [MandateValue] IReadOnlyList<string>? cc,
        [MandateValue] IReadOnlyList<string>? bcc,
        [MandateValue] bool? includeDeclarer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);

        this.host = host;
        this.port = port;
        this.sender = ParseAddress(sender, nameof(sender));
        this.encryption = encryption ?? MailEncryption.StartTls;

        // shortcut: a login with username and password only; OAuth comes once a mail server demands it.
        if (string.IsNullOrEmpty(username) != string.IsNullOrEmpty(password))
            throw new ArgumentException("A login needs both a username and a password.", nameof(username));

        login = string.IsNullOrEmpty(username) ? null : new NetworkCredential(username, password);
        if (login is not null && this.encryption == MailEncryption.None)
            throw new ArgumentException("A login needs an encrypted connection: set the encryption to StartTls or SslOnConnect.", nameof(encryption));

        this.template = MailTemplate.Parse(template);
        this.to = ParseAddresses(to, nameof(to));
        this.cc = ParseAddresses(cc, nameof(cc));
        this.bcc = ParseAddresses(bcc, nameof(bcc));
        this.includeDeclarer = includeDeclarer ?? false;
    }

    /// <inheritdoc />
    public async Task<ChannelResult> DeliverAsync(DeliveryInfo delivery, IReadOnlyList<IDeliveryFile> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(files);

        using var message = BuildMessage(delivery, files.Select(file => file.Name).ToList());
        var recipients = message.To.Count + message.Cc.Count + message.Bcc.Count;
        if (recipients == 0)
            return ChannelResult.Success("The mail has no recipient, so none was sent.");

        try
        {
            // The client leaves without a goodbye: once the server took the mail, a failing goodbye would only fail the
            // attempt and send the mail a second time.
            using var client = new SmtpClient { LocalDomain = sender.Domain };
            await client.ConnectAsync(host, port, ToSocketOptions(encryption), cancellationToken).ConfigureAwait(false);
            if (login is not null)
                await client.AuthenticateAsync(login, cancellationToken).ConfigureAwait(false);

            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (SmtpCommandException ex) when (ex.ErrorCode == SmtpErrorCode.RecipientNotAccepted)
        {
            return ChannelResult.Failure($"The mail server refused the recipient <{ex.Mailbox?.Address}>, so the mail went to nobody: {ex.Message}");
        }
        catch (Exception ex) when (ex is CommandException or ProtocolException or AuthenticationException or SslHandshakeException or NotSupportedException or IOException or SocketException)
        {
            return ChannelResult.Failure($"The mail could not be sent through <{host}:{port}>: {ex.Message}");
        }

        return ChannelResult.Success(recipients == 1 ? "The mail went to 1 recipient." : $"The mail went to {recipients} recipients.");
    }

    /// <summary>
    /// Builds the mail for a delivery: the template filled in, the recipients of the mandate and, with the switch, the
    /// declarer, and a Message-ID that every attempt repeats.
    /// </summary>
    internal MimeMessage BuildMessage(DeliveryInfo delivery, IReadOnlyList<string> fileNames)
    {
        var message = new MimeMessage
        {
            MessageId = $"{delivery.JobId}.{delivery.ReactionId}@{sender.Domain}",
            Subject = template.RenderSubject(delivery, fileNames, TimeZoneInfo.Local),
            Body = new TextPart(TextFormat.Html) { Text = template.RenderBody(delivery, fileNames, TimeZoneInfo.Local) },
        };
        message.From.Add(sender);
        message.To.AddRange(to);
        if (includeDeclarer && DeclarerAddress(delivery.Declarer) is MailboxAddress declarer)
            message.To.Add(declarer);

        message.Cc.AddRange(cc);
        message.Bcc.AddRange(bcc);
        return message;
    }

    // Only a person has an address; a machine client never gets the mail.
    private static MailboxAddress? DeclarerAddress(DeclarerInfo declarer)
        => declarer.Kind == DeclarerKind.User && TryParseAddress(declarer.Email, out var mailbox)
            ? new MailboxAddress(declarer.Name, mailbox.Address)
            : null;

    private static List<MailboxAddress> ParseAddresses(IReadOnlyList<string>? addresses, string parameterName)
        => (addresses ?? []).Select(address => ParseAddress(address, parameterName)).ToList();

    private static MailboxAddress ParseAddress(string? address, string parameterName)
        => TryParseAddress(address, out var mailbox)
            ? mailbox
            : throw new ArgumentException($"<{address}> is not a mail address.", parameterName);

    // A mail address needs a domain; the parser alone would also take a bare name.
    private static bool TryParseAddress(string? address, [NotNullWhen(true)] out MailboxAddress? mailbox)
    {
        mailbox = null;
        return !string.IsNullOrWhiteSpace(address) && MailboxAddress.TryParse(address, out mailbox) && !string.IsNullOrEmpty(mailbox.Domain);
    }

    private static SecureSocketOptions ToSocketOptions(MailEncryption encryption) => encryption switch
    {
        MailEncryption.StartTls => SecureSocketOptions.StartTls,
        MailEncryption.SslOnConnect => SecureSocketOptions.SslOnConnect,
        MailEncryption.None => SecureSocketOptions.None,
        _ => throw new ArgumentOutOfRangeException(nameof(encryption), encryption, null),
    };
}
