using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Channels.Mail;
using Geopilot.Pipeline.Config;

namespace Geopilot.Pipeline.Test.Channels;

[TestClass]
public class MailChannelTest
{
    private const string Implementation = "Geopilot.Pipeline.Channels.Mail.MailChannel";
    private const string Template = "<html><head><title>Lieferung {{delivery_id}}</title></head><body>{{files}}</body></html>";

    private static readonly DeliveryInfo Delivery = new()
    {
        Id = 42,
        JobId = Guid.NewGuid(),
        ReactionId = "info_mail",
        DeclaredAt = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero),
        Mandate = new MandateInfo { Id = 7, Name = new Dictionary<string, string> { ["de"] = "Mandat A" } },
        Declarer = new DeclarerInfo { Kind = DeclarerKind.User, Name = "Lieferant", Email = "lieferant@example.com" },
    };

    private static readonly DeclarerInfo MachineClient = new() { Kind = DeclarerKind.MachineClient, Name = "Fachschale" };

    [TestMethod]
    public void BuildMessageAddressesTheRecipientsAndTheDeclarer()
    {
        var channel = Channel(to: ["empfang@example.com"], cc: ["kopie@example.com"], bcc: ["archiv@example.com"], includeDeclarer: true);

        using var message = channel.BuildMessage(Delivery, ["daten.xtf"]);

        Assert.AreEqual("geopilot@geopilot.example", message.From.Mailboxes.Single().Address);
        CollectionAssert.AreEqual(new[] { "empfang@example.com", "lieferant@example.com" }, message.To.Mailboxes.Select(mailbox => mailbox.Address).ToList());
        Assert.AreEqual("Lieferant", message.To.Mailboxes.Last().Name);
        Assert.AreEqual("kopie@example.com", message.Cc.Mailboxes.Single().Address);
        Assert.AreEqual("archiv@example.com", message.Bcc.Mailboxes.Single().Address);
        Assert.AreEqual("Lieferung 42", message.Subject);
        Assert.AreEqual("<html><head><title>Lieferung 42</title></head><body><ul><li>daten.xtf</li></ul></body></html>", message.HtmlBody);
    }

    [TestMethod]
    public void BuildMessageRepeatsTheMessageIdOfTheReaction()
    {
        var channel = Channel(to: ["empfang@example.com"]);

        using var first = channel.BuildMessage(Delivery, []);
        using var second = channel.BuildMessage(Delivery, []);

        Assert.AreEqual($"{Delivery.JobId}.info_mail@geopilot.example", first.MessageId);
        Assert.AreEqual(first.MessageId, second.MessageId);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Switch off")]
    [DataRow(true, DisplayName = "Machine client")]
    public void BuildMessageLeavesOutTheDeclarer(bool includeDeclarer)
    {
        var channel = Channel(to: ["empfang@example.com"], includeDeclarer: includeDeclarer);

        using var message = channel.BuildMessage(includeDeclarer ? Delivery with { Declarer = MachineClient } : Delivery, []);

        Assert.AreEqual("empfang@example.com", message.To.Mailboxes.Single().Address);
    }

    [TestMethod]
    public async Task DeliverAsyncSendsNothingWithoutARecipient()
    {
        // Nothing listens on the port: the channel must not even connect.
        var channel = Channel(includeDeclarer: true, port: 9);

        var result = await channel.DeliverAsync(Delivery with { Declarer = MachineClient }, [], CancellationToken.None);

        Assert.AreEqual(ChannelResult.Success("The mail has no recipient, so none was sent."), result);
    }

    [TestMethod]
    [DataRow("kein-mail", null, null, true, "<kein-mail> is not a mail address. (Parameter 'to')", DisplayName = "Address without domain")]
    [DataRow("empfang@example.com", "geopilot", null, true, "A login needs both a username and a password. (Parameter 'username')", DisplayName = "Username without password")]
    [DataRow("empfang@example.com", "geopilot", "geheim", false, "A login needs an encrypted connection: set the encryption to StartTls or SslOnConnect. (Parameter 'encryption')", DisplayName = "Login without encryption")]
    public void ConstructorRefusesAnInvalidConfiguration(string to, string? username, string? password, bool encrypted, string expected)
    {
        var encryption = encrypted ? MailEncryption.StartTls : MailEncryption.None;
        var exception = Assert.ThrowsExactly<ArgumentException>(() => Channel(to: [to], username: username, password: password, encryption: encryption));

        Assert.AreEqual(expected, exception.Message);
    }

    [TestMethod]
    public void ValidateStopsTheStartForATemplateWithoutATitle()
    {
        var resources = Path.Combine(Path.GetTempPath(), $"geopilot-resources-{Guid.NewGuid()}");
        Directory.CreateDirectory(resources);
        File.WriteAllText(Path.Combine(resources, "info.html"), "<html><body>{{delivery_id}}</body></html>");
        var factory = new DeliveryChannelFactory(
            new Dictionary<string, Parameterization> { [Implementation] = new() { ["host"] = "mail.example.com", ["port"] = "587", ["sender"] = "geopilot@geopilot.example" } },
            resources);

        var error = Assert.ContainsSingle(factory.Validate(Implementation, new Parameterization { ["template"] = "info.html" }, ["to", "includeDeclarer"]));

        Assert.AreEqual($"Channel <{Implementation}>: The template has no <title> with text, which the mail takes as its subject. (Parameter 'template')", error);
    }

    private static MailChannel Channel(
        IReadOnlyList<string>? to = null,
        IReadOnlyList<string>? cc = null,
        IReadOnlyList<string>? bcc = null,
        bool? includeDeclarer = null,
        string? username = null,
        string? password = null,
        MailEncryption encryption = MailEncryption.StartTls,
        int port = 587)
        => new("mail.example.com", port, "geopilot@geopilot.example", username, password, encryption, Template, to, cc, bcc, includeDeclarer);
}
