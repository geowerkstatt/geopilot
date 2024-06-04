using Microsoft.EntityFrameworkCore;
using Stac;
using Stac.Api.Interfaces;

namespace Geopilot.Api.StacServices;

/// <summary>
/// Provides access to STAC items.
/// </summary>
public class StacItemsProvider : IItemsProvider
{
    private readonly ILogger<StacItemsProvider> logger;
    private readonly IDbContextFactory<Context> contextFactory;
    private readonly StacConverter stacConverter;

    /// <summary>
    /// Initializes a new instance of the <see cref="StacItemsProvider"/> class.
    /// </summary>
    public StacItemsProvider(ILogger<StacItemsProvider> logger, IDbContextFactory<Context> contextFactory, StacConverter stacConverter)
    {
        this.logger = logger;
        this.contextFactory = contextFactory;
        this.stacConverter = stacConverter;
    }

    /// <inheritdoc/>
    public bool AnyItemsExist(IEnumerable<StacItem> items, IStacApiContext stacApiContext)
    {
        using var db = contextFactory.CreateDbContext();
        foreach (var collection in stacApiContext.Collections)
        {
            try
            {
                return db.MandatesWithIncludes.FirstOrDefault(m => stacConverter.GetCollectionId(m) == collection)?.Deliveries?.Any() ?? false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while checking if items exist.");
                return false;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public Task<StacItem> GetItemByIdAsync(string featureId, IStacApiContext stacApiContext, CancellationToken cancellationToken)
    {
        try
        {
            using var db = contextFactory.CreateDbContext();
            var delivery = db.DeliveriesWithIncludes
                .FirstOrDefault(d => stacConverter.GetItemId(d) == featureId && (stacConverter.GetCollectionId(d.Mandate) == stacApiContext.Collections.First()))
                ?? throw new InvalidOperationException($"Item with id {featureId} does not exist.");
            var item = stacConverter.ToStacItem(delivery);
            return Task.FromResult(item);
        }
        catch (Exception ex)
        {
            var message = $"Error while getting item with id {featureId}.";
            logger.LogError(ex, message);
            throw new InvalidOperationException(message, ex);
        }
    }

    /// <inheritdoc/>
    public string GetItemEtag(string featureId, IStacApiContext stacApiContext)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<IEnumerable<StacItem>> GetItemsAsync(IStacApiContext stacApiContext, CancellationToken cancellationToken)
    {
        IEnumerable<StacItem> items = new List<StacItem>();

        var collectionIds = stacApiContext.Collections?.ToList();
        using var db = contextFactory.CreateDbContext();
        var mandates = db.MandatesWithIncludes;

        if (collectionIds?.Any() == true)
        {
            mandates = mandates.FindAll(m => collectionIds.Contains(stacConverter.GetCollectionId(m)));
        }

        mandates.ToList().ForEach(m => items.Concat(m.Deliveries.Select(d => stacConverter.ToStacItem(d))));
        return Task.FromResult(items);
    }
}
