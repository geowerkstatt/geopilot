namespace Geopilot.Api.Pipeline;

/// <summary>
/// Configuration helpers for <see cref="PipelineOptions"/>.
/// </summary>
public static class PipelineConfigurationExtensions
{
    private const string PluginsKey = "Pipeline:Plugins";

    /// <summary>
    /// Allows <c>Pipeline:Plugins</c> to be supplied as a single comma-separated string (e.g. via env var
    /// <c>Pipeline__Plugins=a.dll,b.dll</c>). When such a scalar value is present, it fully overrides any list bound
    /// from the JSON array form in <c>appsettings.json</c>.
    /// </summary>
    public static IServiceCollection AddPipelinePluginsScalarOverride(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return services.PostConfigure<PipelineOptions>(options =>
        {
            var scalar = configuration[PluginsKey];
            if (string.IsNullOrWhiteSpace(scalar))
                return;

            options.Plugins = scalar
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        });
    }
}
