using Geopilot.Api.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace Geopilot.Api.Test;

[TestClass]
public sealed class OpenApiLocalizedTextSchemaTest
{
    private static JwtTestApp app;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        app = new JwtTestApp();

        // Trigger host initialization so services are available.
        _ = app.Server;
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        app?.Dispose();
    }

    [TestMethod]
    public void PipelineSummaryDisplayNameStaysAStringMap()
    {
        var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();
        var document = swaggerProvider.GetSwagger("v1");

        var schemas = document.Components?.Schemas;
        Assert.IsNotNull(schemas, "OpenAPI document must have component schemas.");

        Assert.IsFalse(
            schemas.ContainsKey("LocalizedText"),
            "LocalizedText must not appear as its own schema component.");

        Assert.IsTrue(
            schemas.ContainsKey("PipelineSummary"),
            "PipelineSummary schema must be present.");

        var properties = schemas["PipelineSummary"].Properties;
        Assert.IsNotNull(properties, "PipelineSummary must have properties.");

        Assert.IsTrue(
            properties.TryGetValue("displayName", out var displayName),
            "PipelineSummary must have a displayName property.");

        Assert.AreEqual(
            JsonSchemaType.Object,
            displayName!.Type,
            "displayName must be of type object.");

        Assert.IsNotNull(
            displayName.AdditionalProperties,
            "displayName must have additionalProperties.");

        Assert.AreEqual(
            JsonSchemaType.String,
            displayName.AdditionalProperties.Type,
            "displayName additionalProperties must be of type string.");
    }
}
