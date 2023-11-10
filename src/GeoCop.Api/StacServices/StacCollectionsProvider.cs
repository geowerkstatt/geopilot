using Microsoft.EntityFrameworkCore;
using Stac;
using Stac.Api.Interfaces;
using Stac.Api.WebApi.Implementations.Default;

namespace GeoCop.Api.StacServices
{
    /// <summary>
    /// Provides access to STAC collections.
    /// </summary>
    public class StacCollectionsProvider : ICollectionsProvider
    {
        private readonly IDbContextFactory<Context> contextFactory;
        private readonly StacConverter stacConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="StacCollectionsProvider"/> class.
        /// </summary>
        /// <param name="contextFactory"></param>
        /// <param name="stacConverter"></param>
        public StacCollectionsProvider(IDbContextFactory<Context> contextFactory, StacConverter stacConverter)
        {
            this.contextFactory = contextFactory;
            this.stacConverter = stacConverter;
        }

        /// <inheritdoc/>
        public Task<StacCollection> GetCollectionByIdAsync(string collectionId, IStacApiContext stacApiContext, CancellationToken cancellationToken = default)
        {
            try
            {
                using var db = contextFactory.CreateDbContext();
                var deliveryMandate = db.DeliveryMandatesWithIncludes.First(dm => (StacConverter.CollectionIdPrefix + dm.Id) == collectionId);
                var collection = stacConverter.ToStacCollection(deliveryMandate);
                return Task.FromResult(collection);
            }
            catch (Exception ex)
            {
                return Task.FromResult<StacCollection>(null);
            }
        }

        /// <inheritdoc/>
        public Task<IEnumerable<StacCollection>> GetCollectionsAsync(IStacApiContext stacApiContext, CancellationToken cancellationToken = default)
        {
            using var db = contextFactory.CreateDbContext();
            var collections = db.DeliveryMandatesWithIncludes.Select(stacConverter.ToStacCollection);
            stacApiContext.Properties.SetProperty(DefaultConventions.MatchedCountPropertiesKey, collections.Count());

            return Task.FromResult(collections);
        }
    }
}
