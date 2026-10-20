using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Microsoft.Extensions.Options;

namespace Geopilot.Api;

/// <summary>
/// Configuration helpers for <see cref="PipelineOptions"/>.
/// </summary>
public static class PipelineConfigurationExtensions
{
    private const string PluginsKey = "Pipeline:Plugins";

    /// <summary>
    /// Binds <see cref="PipelineOptions"/> from <c>Pipeline</c>, including the scalar form of <c>Pipeline:Plugins</c>,
    /// and checks the host values of the delivery channels when the application starts.
    /// </summary>
    public static IServiceCollection AddPipelineOptions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<PipelineOptions>(configuration.GetSection("Pipeline"));
        services.AddPipelinePluginsScalarOverride(configuration);
        services.AddSingleton<IValidateOptions<PipelineOptions>, FileDropDirectoryValidator>();
        services.AddOptions<PipelineOptions>().ValidateOnStart();
        return services;
    }

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
