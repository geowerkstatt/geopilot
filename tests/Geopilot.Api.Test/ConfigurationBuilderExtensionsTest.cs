using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Geopilot.Api;

/// <summary>
/// Each test uses its own configuration key: environment variables are process global, so a shared key
/// would let the environment variable test bleed into the others when tests run in parallel. The keys are
/// flat so they can be set through an environment variable of the same name.
/// </summary>
[TestClass]
public sealed class ConfigurationBuilderExtensionsTest
{
    [TestMethod]
    public void AddDeveloperOverlaysOverridesAppsettingsInDevelopment()
    {
        const string key = "OverlayProbeDevelopment";
        var contentRoot = CreateContentRoot(
            ("appsettings.json", key, "base"),
            ("appsettings.Local.json", key, "overlay"));
        try
        {
            var builder = CreateBuilder(contentRoot, Environments.Development);

            builder.AddDeveloperOverlays();

            Assert.AreEqual("overlay", builder.Configuration[key]);
        }
        finally
        {
            Directory.Delete(contentRoot, true);
        }
    }

    [TestMethod]
    public void AddDeveloperOverlaysLoadsNamedOverlays()
    {
        var contentRoot = CreateContentRoot(
            ("appsettings.Local.first.json", "OverlayProbeFirstPlugin", "first"),
            ("appsettings.Local.second.json", "OverlayProbeSecondPlugin", "second"));
        try
        {
            var builder = CreateBuilder(contentRoot, Environments.Development);

            builder.AddDeveloperOverlays();

            Assert.AreEqual("first", builder.Configuration["OverlayProbeFirstPlugin"]);
            Assert.AreEqual("second", builder.Configuration["OverlayProbeSecondPlugin"]);
        }
        finally
        {
            Directory.Delete(contentRoot, true);
        }
    }

    /// <summary>
    /// The overlays are inserted before the application environment variable source, so deployment
    /// configuration (for example the environment variables set in docker-compose) keeps precedence.
    /// </summary>
    [TestMethod]
    public void AddDeveloperOverlaysLosesToEnvironmentVariables()
    {
        const string key = "OverlayProbeEnvironment";
        var contentRoot = CreateContentRoot(
            ("appsettings.json", key, "base"),
            ("appsettings.Local.json", key, "overlay"));
        Environment.SetEnvironmentVariable(key, "environment");
        try
        {
            var builder = CreateBuilder(contentRoot, Environments.Development);

            builder.AddDeveloperOverlays();

            Assert.AreEqual("environment", builder.Configuration[key]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
            Directory.Delete(contentRoot, true);
        }
    }

    [TestMethod]
    public void AddDeveloperOverlaysIgnoresOverlaysOutsideDevelopment()
    {
        const string key = "OverlayProbeProduction";
        var contentRoot = CreateContentRoot(
            ("appsettings.json", key, "base"),
            ("appsettings.Local.json", key, "overlay"));
        try
        {
            var builder = CreateBuilder(contentRoot, Environments.Production);

            builder.AddDeveloperOverlays();

            Assert.AreEqual("base", builder.Configuration[key]);
        }
        finally
        {
            Directory.Delete(contentRoot, true);
        }
    }

    private static WebApplicationBuilder CreateBuilder(string contentRoot, string environmentName)
    {
        return WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRoot,
            EnvironmentName = environmentName,
        });
    }

    private static string CreateContentRoot(params (string FileName, string Key, string Value)[] files)
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"overlayTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);

        foreach (var (fileName, key, value) in files)
        {
            File.WriteAllText(Path.Combine(contentRoot, fileName), $$"""{ "{{key}}": "{{value}}" }""");
        }

        return contentRoot;
    }
}
