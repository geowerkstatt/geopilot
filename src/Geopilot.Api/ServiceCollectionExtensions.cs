using Geopilot.Api.Pipeline;
using Geopilot.Api.StacServices;
using Stac.Api.Interfaces;
using Stac.Api.Models.Extensions.Sort.Context;
using Stac.Api.Services.Debugging;
using Stac.Api.Services.Default;
using Stac.Api.WebApi.Extensions;
using Stac.Api.WebApi.Services;
using Stac.Api.WebApi.Services.Context;

namespace Geopilot.Api;

/// <summary>
/// Provides extension methods for registering pipeline and STAC-related services with an IServiceCollection for
/// dependency injection.
/// </summary>
/// <remarks>These extension methods simplify the setup of pipeline processing and STAC data services in
/// applications using dependency injection. Use these methods during application startup to ensure all required
/// services are registered and configured appropriately.</remarks>
public static class ServiceCollectionExtensions
{
    private static string pipelineDefinitionKey = "Pipeline:Definition";

    /// <summary>
    /// Registers an IPipelineFactory implementation with the dependency injection container using the pipeline
    /// definition specified in the application configuration.
    /// </summary>
    /// <remarks>This method expects a configuration value at the key 'Pipeline:Definition' that specifies the
    /// pipeline definition file. The pipeline configuration is validated during registration, and any validation errors
    /// will prevent the application from starting.</remarks>
    /// <param name="services">The IServiceCollection to which the IPipelineFactory singleton will be added. Cannot be null.</param>
    /// <exception cref="InvalidOperationException">Thrown if the pipeline definition is missing from the configuration or if the pipeline configuration contains
    /// validation errors.</exception>
    public static void AddPipelineFactory(this IServiceCollection services)
    {
        Func<IServiceProvider, IPipelineFactory> cofigurePipelineFactory = (IServiceProvider sp) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var pipelineDefinition = configuration.GetValue<string>(pipelineDefinitionKey);

            if (string.IsNullOrWhiteSpace(pipelineDefinition))
                throw new InvalidOperationException($"Path to pipeline definition not specified. Define path to pipeline definition under <{pipelineDefinitionKey}>.");

            if (!File.Exists(pipelineDefinition))
                throw new InvalidOperationException($"Pipeline definition file not found at path: {pipelineDefinition}");

            var pipelineFactory = PipelineFactory.Builder()
                    .File(pipelineDefinition)
                    .Configuration(configuration)
                    .Build();

            return pipelineFactory;
        };
        services.AddSingleton<IPipelineFactory>(cofigurePipelineFactory);
    }

    /// <summary>
    /// Adds services required for STAC.
    /// </summary>
    public static IServiceCollection AddStacData(this IServiceCollection services, Action<IStacWebApiBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.AddStacWebApi();

        // Add the Http Stac Api context factory
        services.AddSingleton<IStacApiContextFactory, HttpsStacApiContextFactory>();

        // Add the default context filters provider
        services.AddSingleton<IStacApiContextFiltersProvider, DefaultStacContextFiltersProvider>();

        // Register the HTTP pagination filter
        services.AddSingleton<IStacApiContextFilter, HttpPaginator>();

        // Register the sorting filter
        services.AddSingleton<IStacApiContextFilter, SortContextFilter>();

        // Register the debug filter
        services.AddSingleton<IStacApiContextFilter, DebugContextFilter>();

        // Register the default collections provider
        // TODO: Replace StacLinker with CollectionBasedStacLinker once https://github.com/Terradue/DotNetStac.Api/issues/1 is resolved
        services.AddSingleton<IStacLinker, StacLinker>();

        // Add the default controllers
        services.AddDefaultControllers();

        // Add the default extensions
        services.AddDefaultStacApiExtensions();

        // Add converters to create Stac Objects
        services.AddTransient<StacConverter>();

        // Add the stac data services
        services.AddSingleton<IDataServicesProvider, StacDataServicesProvider>();

        // Add the stac root catalog provider
        services.AddSingleton<IRootCatalogProvider, StacRootCatalogProvider>();

        // Add the stac collections provider
        services.AddSingleton<ICollectionsProvider, StacCollectionsProvider>();

        // Add the stac items provider
        services.AddSingleton<IItemsProvider, StacItemsProvider>();

        // Add the stac items broker
        services.AddSingleton<IItemsBroker, StacItemsBroker>();

        var builder = new StacWebApiBuilder(services);
        configure(builder);
        return services;
    }
}
