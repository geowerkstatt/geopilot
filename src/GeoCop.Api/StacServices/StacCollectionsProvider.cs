using Stac;
using Stac.Api.Interfaces;
using Stac.Collection;
using System.Globalization;

namespace GeoCop.Api.StacServices
{
    /// <summary>
    /// Provides access to STAC collections.
    /// </summary>
    public class StacCollectionsProvider : ICollectionsProvider
    {
        /// <inheritdoc/>
        public Task<StacCollection> GetCollectionByIdAsync(string collectionId, IStacApiContext stacApiContext, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<IEnumerable<StacCollection>> GetCollectionsAsync(IStacApiContext stacApiContext, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
