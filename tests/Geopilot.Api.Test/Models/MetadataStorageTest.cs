using Geopilot.PipelineCore.Pipeline;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Geopilot.Api.Models;

[TestClass]
public class MetadataStorageTest
{
    private Context context;

    [TestInitialize]
    public void Initialize() => context = AssemblyInitialize.DbFixture.GetTestContext();

    [TestCleanup]
    public void Cleanup() => context.Dispose();

    [TestMethod]
    public async Task StoresTheDefinitionsInTheShapeTheApiWrites()
    {
        var mandate = context.Mandates.Add(new Mandate
        {
            Name = TestHelpers.Localized(nameof(StoresTheDefinitionsInTheShapeTheApiWrites)),
            MetadataFields =
            [
                new MetadataField(
                    "responsible",
                    new LocalizedText(new Dictionary<string, string> { { "de", "Zuständig" }, { "en", "Responsible" } }),
                    MetadataFieldType.Selection,
                    FieldEvaluationType.Required,
                    [
                        new MetadataFieldOption("canton", TestHelpers.Localized("Kanton")),
                        new MetadataFieldOption("municipality", TestHelpers.Localized("Gemeinde")),
                    ]),
                new MetadataField("project_number", TestHelpers.Localized("Projektnummer"), MetadataFieldType.Text, FieldEvaluationType.Optional),
                new MetadataField("pieces", TestHelpers.Localized("Anzahl"), MetadataFieldType.WholeNumber, FieldEvaluationType.Optional),
                new MetadataField("area", TestHelpers.Localized("Fläche"), MetadataFieldType.DecimalNumber, FieldEvaluationType.Optional),
            ],
        }).Entity;
        await context.SaveChangesAsync();

        var json = await context.Database
            .SqlQuery<string>($"SELECT \"MetadataFields\"::text AS \"Value\" FROM \"Mandates\" WHERE \"Id\" = {mandate.Id}")
            .SingleAsync();

        using var document = JsonDocument.Parse(json);
        var fields = document.RootElement;
        Assert.AreEqual("responsible", fields[0].GetProperty("id").GetString());
        Assert.AreEqual("selection", fields[0].GetProperty("type").GetString());
        Assert.AreEqual("required", fields[0].GetProperty("evaluation").GetString());
        Assert.AreEqual("Zuständig", fields[0].GetProperty("title").GetProperty("de").GetString());
        Assert.AreEqual("municipality", fields[0].GetProperty("options")[1].GetProperty("id").GetString());
        Assert.AreEqual("text", fields[1].GetProperty("type").GetString());
        Assert.AreEqual("integer", fields[2].GetProperty("type").GetString());
        Assert.AreEqual("decimal", fields[3].GetProperty("type").GetString());
    }

    [TestMethod]
    public async Task ReadsTheCopyOfADeliveryBackWithItsValues()
    {
        var client = context.MachineClients.Add(new MachineClient { AuthIdentifier = Guid.NewGuid().ToString(), Name = "QUIETHARBOR" }).Entity;
        var mandate = context.Mandates.Add(new Mandate { Name = TestHelpers.Localized(nameof(ReadsTheCopyOfADeliveryBackWithItsValues)) }).Entity;
        await context.SaveChangesAsync();

        var options = new List<MetadataFieldOption>
        {
            new("canton", TestHelpers.Localized("Kanton")),
            new("municipality", TestHelpers.Localized("Gemeinde")),
        };
        var delivery = context.Deliveries.Add(new Delivery
        {
            JobId = Guid.NewGuid(),
            Mandate = mandate,
            DeclaringClient = client,
            Metadata =
            [
                new DeliveredMetadataField("responsible", TestHelpers.Localized("Zuständig"), MetadataFieldType.Selection, FieldEvaluationType.Required, options, JsonSerializer.SerializeToElement("canton")),
                new DeliveredMetadataField("project_number", TestHelpers.Localized("Projektnummer"), MetadataFieldType.Text, FieldEvaluationType.Optional, null, null),
            ],
        }).Entity;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var read = await context.Deliveries.AsNoTracking().SingleAsync(d => d.Id == delivery.Id);

        Assert.AreEqual("canton", read.Metadata[0].Value?.GetString());
        Assert.AreEqual(2, read.Metadata[0].Options!.Count);
        Assert.IsNull(read.Metadata[1].Value);
    }
}
