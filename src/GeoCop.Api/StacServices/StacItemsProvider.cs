using Stac;
using Stac.Api.Interfaces;

namespace GeoCop.Api.StacServices
{
    /// <summary>
    /// Provides access to STAC items.
    /// </summary>
    public class StacItemsProvider : IItemsProvider
    {
        /// <inheritdoc/>
        public bool AnyItemsExist(IEnumerable<StacItem> items, IStacApiContext stacApiContext)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<StacItem> GetItemByIdAsync(string featureId, IStacApiContext stacApiContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public string GetItemEtag(string featureId, IStacApiContext stacApiContext)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<IEnumerable<StacItem>> GetItemsAsync(IStacApiContext stacApiContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
