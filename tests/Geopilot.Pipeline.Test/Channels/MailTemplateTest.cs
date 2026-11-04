using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Channels.Mail;

namespace Geopilot.Pipeline.Test.Channels;

[TestClass]
public class MailTemplateTest
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    private static readonly DeliveryInfo Delivery = new()
    {
        Id = 42,
        JobId = Guid.NewGuid(),
        ReactionId = "info_mail",
        DeclaredAt = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero),
        Comment = "Erste Zeile & <b>fett</b>\nZweite Zeile",
        Mandate = new MandateInfo { Id = 7, Name = new Dictionary<string, string> { ["de"] = "Nachführung AV", ["fr"] = "Mise à jour MO" } },
        Declarer = new DeclarerInfo { Kind = DeclarerKind.User, Name = "Muster & Partner <AG>", Email = "lieferant@example.com" },
    };

    private static readonly string[] FileNames = ["daten.xtf", "<bericht>.pdf"];

    [TestMethod]
    [DataRow("<html><body>Neue Lieferung</body></html>", DisplayName = "No title")]
    [DataRow("<html><head><title></title></head></html>", DisplayName = "Empty title")]
    [DataRow("<html><head><title>  </title></head></html>", DisplayName = "Blank title")]
    public void ParseRefusesATemplateWithoutASubject(string html)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => MailTemplate.Parse(html));
        Assert.StartsWith("The template has no <title> with text, which the mail takes as its subject.", exception.Message);
    }

    [TestMethod]
    public void ParseRefusesUnknownPlaceholders()
    {
        var html = "<title>{{deliveryid}}</title>{{delivery_id:de}} {{ comment }} {{deliveryid}} {{mandate_name:de}} {{metadata:deu}} {{metadatas}} {{metadata:}}";

        var exception = Assert.ThrowsExactly<ArgumentException>(() => MailTemplate.Parse(html));
        Assert.StartsWith("The template uses unknown placeholders: {{deliveryid}}, {{delivery_id:de}}, {{ comment }}, {{metadata:deu}}, {{metadatas}}, {{metadata:}}.", exception.Message);
    }

    [TestMethod]
    public void RenderSubjectFillsInPlainTextOnOneLine()
    {
        var template = MailTemplate.Parse("<title>Lieferung {{delivery_id}} f&uuml;r {{mandate_name:fr}}: {{comment}}</title>");

        Assert.AreEqual(
            "Lieferung 42 für Mise à jour MO: Erste Zeile & <b>fett</b> Zweite Zeile",
            template.RenderSubject(Delivery, FileNames, Zurich));
    }

    [TestMethod]
    public void RenderBodyEncodesEveryValueForHtml()
    {
        var template = MailTemplate.Parse("<title>Neue Lieferung</title><p>{{declarer_name}}</p><p>{{comment}}</p>{{files}}");

        Assert.AreEqual(
            "<title>Neue Lieferung</title><p>Muster &amp; Partner &lt;AG&gt;</p><p>Erste Zeile &amp; &lt;b&gt;fett&lt;/b&gt;<br>Zweite Zeile</p>"
                + "<ul><li>daten.xtf</li><li>&lt;bericht&gt;.pdf</li></ul>",
            template.RenderBody(Delivery, FileNames, Zurich));
    }

    [TestMethod]
    [DataRow(2026, 10, 20, "20.10.2026 10:00", DisplayName = "Summer time")]
    [DataRow(2026, 11, 20, "20.11.2026 09:00", DisplayName = "Winter time")]
    public void RenderBodyGivesTheDeclarationInLocalTime(int year, int month, int day, string expected)
    {
        var template = MailTemplate.Parse("<title>{{declared_at}}</title>");
        var delivery = Delivery with { DeclaredAt = new DateTimeOffset(year, month, day, 8, 0, 0, TimeSpan.Zero) };

        Assert.AreEqual($"<title>{expected}</title>", template.RenderBody(delivery, FileNames, Zurich));
    }

    [TestMethod]
    public void RenderBodyFallsBackForTheNameOfTheMandate()
    {
        var template = MailTemplate.Parse("<title>{{mandate_name:it}} / {{mandate_name}}</title>");

        // Without Italian and English the name follows the order of the STAC titles: German comes next. The encoding
        // writes an umlaut as a number, which every mail program shows as the letter.
        Assert.AreEqual("<title>Nachf&#252;hrung AV / Nachf&#252;hrung AV</title>", template.RenderBody(Delivery, FileNames, Zurich));
    }

    private static readonly MetadataInfo[] Metadata =
    [
        new() { Title = new Dictionary<string, string> { ["de"] = "Projektnummer", ["en"] = "Project number" }, Texts = ["2026-117"] },
        new() { Title = new Dictionary<string, string> { ["de"] = "Verantwortlich", ["en"] = "Responsible" }, Option = new Dictionary<string, string> { ["de"] = "Kanton", ["en"] = "Canton" } },
        new() { Title = new Dictionary<string, string> { ["en"] = "Municipalities" }, Texts = ["Aarau", "Buchs"] },
        new() { Title = new Dictionary<string, string> { ["en"] = "Parcels" }, Number = 12 },
        new() { Title = new Dictionary<string, string> { ["en"] = "Area" }, Number = 12.5m },
        new() { Title = new Dictionary<string, string> { ["en"] = "Due date" }, Date = new DateOnly(2026, 11, 4) },
    ];

    [TestMethod]
    public void RenderBodyListsTheMetadata()
    {
        var template = MailTemplate.Parse("<title>Neue Lieferung</title>{{metadata:de}}");

        Assert.AreEqual(
            "<title>Neue Lieferung</title><ul><li>Projektnummer: 2026-117</li><li>Verantwortlich: Kanton</li><li>Municipalities: Aarau, Buchs</li><li>Parcels: 12</li><li>Area: 12.5</li><li>Due date: 04.11.2026</li></ul>",
            template.RenderBody(Delivery with { Metadata = Metadata }, FileNames, Zurich));
    }

    [TestMethod]
    public void RenderSubjectJoinsTheMetadataWithCommas()
    {
        var template = MailTemplate.Parse("<title>{{metadata}}</title>");

        Assert.AreEqual(
            "Project number: 2026-117, Responsible: Canton, Municipalities: Aarau, Buchs, Parcels: 12, Area: 12.5, Due date: 04.11.2026",
            template.RenderSubject(Delivery with { Metadata = Metadata }, FileNames, Zurich));
    }

    [TestMethod]
    public void RenderBodyEncodesTheMetadataForHtml()
    {
        var template = MailTemplate.Parse("<title>Neue Lieferung</title>{{metadata}}");
        var metadata = new MetadataInfo { Title = new Dictionary<string, string> { ["en"] = "Note" }, Texts = ["<b>x</b>"] };

        Assert.AreEqual(
            "<title>Neue Lieferung</title><ul><li>Note: &lt;b&gt;x&lt;/b&gt;</li></ul>",
            template.RenderBody(Delivery with { Metadata = [metadata] }, FileNames, Zurich));
    }

    [TestMethod]
    public void RenderBodyLeavesTheMetadataEmptyWithoutFields()
    {
        var template = MailTemplate.Parse("<title>Neue Lieferung</title>[{{metadata}}]");

        Assert.AreEqual("<title>Neue Lieferung</title>[]", template.RenderBody(Delivery, FileNames, Zurich));
    }

    [TestMethod]
    public void ParseAcceptsTheShippedExample()
    {
        // The definition validation finds the file, but without a base configuration it never builds the mail channel.
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Resources", "vorlagen", "info-mail.html"));

        var template = MailTemplate.Parse(html);

        Assert.AreEqual("Neue Lieferung 42 / Nouvelle livraison 42", template.RenderSubject(Delivery, FileNames, Zurich));
    }
}
