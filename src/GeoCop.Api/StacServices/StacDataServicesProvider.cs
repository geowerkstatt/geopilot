using Stac.Api.Interfaces;

namespace GeoCop.Api.StacServices
{
    /// <summary>
    /// Provides access to STAC data services.
    /// </summary>
    public class StacDataServicesProvider : IDataServicesProvider
    {
        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="StacDataServicesProvider"/> class.
        /// </summary>
        public StacDataServicesProvider(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public ICollectionsProvider GetCollectionsProvider() => serviceProvider.GetRequiredService<ICollectionsProvider>();

        /// <inheritdoc/>
        public IItemsBroker GetItemsBroker() => serviceProvider.GetRequiredService<IItemsBroker>();

        /// <inheritdoc/>
        public IItemsProvider GetItemsProvider() => serviceProvider.GetRequiredService<IItemsProvider>();

        /// <inheritdoc/>
        public IRootCatalogProvider GetRootCatalogProvider() => serviceProvider.GetRequiredService<IRootCatalogProvider>();

        /// <inheritdoc/>
        public IStacQueryProvider GetStacQueryProvider(IStacApiContext stacApiContext) => serviceProvider.GetRequiredService<IStacQueryProvider>();
    }
}
