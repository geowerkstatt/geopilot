using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Channels.Mail;
using System.Net.Http.Json;

namespace Geopilot.Pipeline.Test.Channels;

/// <summary>
/// Sends through the Mailpit instances of the compose file and reads what arrived through their API: <c>mailpit</c>
/// without encryption on port 1025, <c>mailpit-tls</c> with STARTTLS and a login on port 1026.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class MailChannelIntegrationTest
{
    private const string Mailpit = "http://localhost:8025";
    private const string MailpitTls = "http://localhost:8026";
    private const string Sender = "geopilot@geopilot.example";
    private const string Template = "<html><head><title>Lieferung {{delivery_id}} für {{mandate_name:de}}</title></head><body><p>{{comment}}</p>{{files}}</body></html>";

    private static readonly HttpClient MailpitApi = new();

    [TestMethod]
    public async Task DeliverAsyncSendsTheMailWithoutEncryption()
    {
        var delivery = Delivery();
        var channel = new MailChannel("localhost", 1025, Sender, null, null, MailEncryption.None, Template, ["empfang@example.com"], null, ["archiv@example.com"], true);

        var result = await channel.DeliverAsync(delivery, [new NamedFile("daten.xtf")], CancellationToken.None);

        Assert.AreEqual(ChannelResult.Success("The mail went to 3 recipients."), result);
        var mail = await FindAsync(Mailpit, delivery);
        Assert.IsNotNull(mail, "The mail did not arrive.");
        Assert.AreEqual("Lieferung 42 für Mandat A", mail.Subject);
        CollectionAssert.AreEqual(new[] { "empfang@example.com", "lieferant@example.com" }, mail.To.Select(address => address.Address).ToList());
        Assert.AreEqual("archiv@example.com", mail.Bcc.Single().Address);
        Assert.Contains("<p>Bitte pr&#252;fen &amp; ablegen</p><ul><li>daten.xtf</li></ul>", mail.Html);
    }

    [TestMethod]
    public async Task DeliverAsyncSendsOverStartTlsWithALogin()
    {
        var delivery = Delivery();
        var channel = new MailChannel("localhost", 1026, Sender, "geopilot", "geopilot", MailEncryption.StartTls, Template, ["empfang@example.com"], null, null, null);

        var result = await channel.DeliverAsync(delivery, [], CancellationToken.None);

        Assert.AreEqual(ChannelResult.Success("The mail went to 1 recipient."), result);
        Assert.IsNotNull(await FindAsync(MailpitTls, delivery), "The mail did not arrive.");
    }

    [TestMethod]
    public async Task DeliverAsyncFailsWithAWrongPassword()
    {
        var delivery = Delivery();
        var channel = new MailChannel("localhost", 1026, Sender, "geopilot", "falsch", MailEncryption.StartTls, Template, ["empfang@example.com"], null, null, null);

        var result = await channel.DeliverAsync(delivery, [], CancellationToken.None);

        Assert.IsFalse(result.Delivered);
        Assert.StartsWith("The mail could not be sent through <localhost:1026>:", result.Message);
        Assert.DoesNotContain("falsch", result.Message);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task DeliverAsyncSendsNothingWhenTheServerRefusesARecipient()
    {
        var delivery = Delivery();
        var channel = new MailChannel("localhost", 1025, Sender, null, null, MailEncryption.None, Template, ["empfang@example.com", "zweite@example.com"], null, null, null);

        // Not parallel to the other tests: the chaos of Mailpit refuses every recipient, of every mail.
        await SetRecipientChaosAsync(100);
        ChannelResult result;
        try
        {
            result = await channel.DeliverAsync(delivery, [], CancellationToken.None);
        }
        finally
        {
            await SetRecipientChaosAsync(0);
        }

        Assert.IsFalse(result.Delivered);
        Assert.StartsWith("The mail server refused the recipient <empfang@example.com>, so the mail went to nobody:", result.Message);
        Assert.IsNull(await FindAsync(Mailpit, delivery), "The mail went out although a recipient was refused.");
    }

    private static DeliveryInfo Delivery() => new()
    {
        Id = 42,
        JobId = Guid.NewGuid(),
        ReactionId = "info_mail",
        DeclaredAt = DateTimeOffset.UtcNow,
        Comment = "Bitte prüfen & ablegen",
        Mandate = new MandateInfo { Id = 7, Name = new Dictionary<string, string> { ["de"] = "Mandat A" } },
        Declarer = new DeclarerInfo { Kind = DeclarerKind.User, Name = "Lieferant", Email = "lieferant@example.com" },
    };

    // The mail is stored once the server accepted it, but the list of Mailpit may lag behind for a moment.
    private static async Task<MailpitMessage?> FindAsync(string api, DeliveryInfo delivery)
    {
        var messageId = $"{delivery.JobId}.{delivery.ReactionId}@geopilot.example";
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var list = await MailpitApi.GetFromJsonAsync<MailpitList>($"{api}/api/v1/messages?limit=500");
            var summary = list?.Messages.FirstOrDefault(message => message.MessageId == messageId);
            if (summary is not null)
                return await MailpitApi.GetFromJsonAsync<MailpitMessage>($"{api}/api/v1/message/{summary.Id}");

            await Task.Delay(200);
        }

        return null;
    }

    private static async Task SetRecipientChaosAsync(int probability)
    {
        using var response = await MailpitApi.PutAsJsonAsync($"{Mailpit}/api/v1/chaos", new { Recipient = new { ErrorCode = 550, Probability = probability } });
        response.EnsureSuccessStatusCode();
    }

    private sealed record MailpitList(IReadOnlyList<MailpitSummary> Messages);

    private sealed record MailpitSummary(string Id, string MessageId);

    private sealed record MailpitMessage(string Subject, string Html, IReadOnlyList<MailpitAddress> To, IReadOnlyList<MailpitAddress> Bcc);

    private sealed record MailpitAddress(string Name, string Address);

    // The mail names the files of a delivery and never reads them.
    private sealed class NamedFile(string name) : IDeliveryFile
    {
        public string Name => name;

        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("The mail channel reads no file.");
    }
}
