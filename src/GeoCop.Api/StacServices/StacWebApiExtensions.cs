using Stac.Api.Interfaces;
using Stac.Api.Models.Extensions.Sort.Context;
using Stac.Api.Services.Debugging;
using Stac.Api.Services.Default;
using Stac.Api.WebApi.Extensions;
using Stac.Api.WebApi.Patterns.CollectionBased;
using Stac.Api.WebApi.Services;
using Stac.Api.WebApi.Services.Context;

namespace GeoCop.Api.StacServices
{
    /// <summary>
    /// Provides access to STAC data services.
    /// </summary>
    public static class StacWebApiExtensions
    {
        /// <summary>
        /// Adds services required for STAC.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IServiceCollection AddStacData(this IServiceCollection services, Action<IStacWebApiBuilder> configure)
        {
            services.AddStacWebApi();

            // Add the Http Stac Api context factory
            services.AddSingleton<IStacApiContextFactory, HttpStacApiContextFactory>();

            // Add the default context filters provider
            services.AddSingleton<IStacApiContextFiltersProvider, DefaultStacContextFiltersProvider>();

            // Register the HTTP pagination filter
            services.AddSingleton<IStacApiContextFilter, HttpPaginator>();

            // Register the sorting filter
            services.AddSingleton<IStacApiContextFilter, SortContextFilter>();

            // Register the debug filter
            services.AddSingleton<IStacApiContextFilter, DebugContextFilter>();

            // Register the default collections provider
            services.AddSingleton<IStacLinker, CollectionBasedStacLinker>();

            // Add the default controllers
            services.AddDefaultControllers();

            // Add the default extensions
            services.AddDefaultStacApiExtensions();

            // Add the stac data services
            services.AddSingleton<IDataServicesProvider, StacDataServicesProvider>();

            // Add the stac root catalog provider
            services.AddScoped<IRootCatalogProvider, StacRootCatalogProvider>();

            // Add the stac collections provider
            services.AddScoped<ICollectionsProvider, StacCollectionsProvider>();

            // Add the stac items provider
            services.AddScoped<IItemsProvider, StacItemsProvider>();

            // Add the stac items broker
            services.AddScoped<IItemsBroker, StacItemsBroker>();

            // Let's Configure
            var builder = new StacWebApiBuilder(services);
            configure(builder);
            return services;
        }
    }
}
