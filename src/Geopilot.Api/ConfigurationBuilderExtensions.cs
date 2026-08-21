using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace Geopilot.Api;

/// <summary>
/// Configuration helpers for local development.
/// </summary>
internal static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds git-ignored developer overlays (<c>appsettings.Local*.json</c>) as JSON configuration sources
    /// for local plugin development. The overlays are inserted right before the application environment-variable
    /// source so environment variables and command-line arguments still take precedence, and they are only
    /// loaded in the Development environment. The files are populated by <c>scripts/link-plugin-config.ps1</c>.
    /// </summary>
    /// <param name="builder">The web application builder whose configuration sources are extended.</param>
    public static void AddDeveloperOverlays(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Environment.IsDevelopment())
            return;

        var overlayFiles = Directory
            .EnumerateFiles(builder.Environment.ContentRootPath, "appsettings.Local*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        if (overlayFiles.Count == 0)
            return;

        // WebApplicationBuilder has several environment-variable sources (host and application). Insert before the
        // last (application) one so the overlays override the appsettings files but lose to environment variables.
        var sources = builder.Configuration.Sources;
        var envIndex = sources.ToList().FindLastIndex(s => s is EnvironmentVariablesConfigurationSource);
        var insertAt = envIndex < 0 ? sources.Count : envIndex;

        foreach (var overlayFile in overlayFiles)
        {
            sources.Insert(insertAt++, new JsonConfigurationSource
            {
                FileProvider = builder.Environment.ContentRootFileProvider,
                Path = Path.GetFileName(overlayFile),
                Optional = true,
                ReloadOnChange = false,
            });
        }
    }

    /// <summary>
    /// Fails fast when the configuration still contains the pre-split CloudStorage section. Its values no
    /// longer bind anywhere and would silently fall back to defaults, so an unmigrated deployment must not start.
    /// </summary>
    /// <param name="configuration">The application configuration to check.</param>
    /// <exception cref="InvalidOperationException">A CloudStorage section is present.</exception>
    public static void EnsureNoLegacyCloudStorageSection(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.GetSection("CloudStorage").Exists())
        {
            throw new InvalidOperationException(
                "The CloudStorage configuration section was replaced: shared upload limits and cleanup moved to Upload, "
                + "Azure-specific settings to Upload:Cloud. Migrate the remaining CloudStorage values and remove the section.");
        }
    }
}
