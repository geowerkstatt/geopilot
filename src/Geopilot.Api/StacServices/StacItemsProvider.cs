using Geopilot.Api.Models;
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
        ArgumentNullException.ThrowIfNull(stacApiContext);

        using var db = contextFactory.CreateDbContext();
        foreach (var collection in stacApiContext.Collections)
        {
            try
            {
                return db.MandatesWithIncludes.FirstOrDefault(m => stacConverter.GetCollectionId(m) == collection)?.Deliveries?.Count > 0;
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
                .AsNoTracking()
                .AsEnumerable()
                .FirstOrDefault(d => stacConverter.GetItemId(d) == featureId && d.Mandate != null && stacConverter.GetCollectionId(d.Mandate) == stacApiContext.Collections.First())
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
    public async Task<IEnumerable<StacItem>> GetItemsAsync(IStacApiContext stacApiContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stacApiContext);

        var items = new List<StacItem>();

        var collectionIds = stacApiContext.Collections?.ToList();
        using var db = contextFactory.CreateDbContext();
        IEnumerable<Mandate> mandates = await db.MandatesWithIncludes.AsNoTracking().ToListAsync(cancellationToken);

        if (collectionIds?.Count > 0)
        {
            mandates = mandates.Where(m => collectionIds.Contains(stacConverter.GetCollectionId(m)));
        }

        foreach (var mandate in mandates)
        {
            items.AddRange(mandate.Deliveries.Select(stacConverter.ToStacItem));
        }

        return items;
    }
}
