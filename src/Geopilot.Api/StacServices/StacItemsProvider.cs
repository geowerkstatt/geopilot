using Geopilot.Api.Models;
using Geopilot.Api.Reactions;
using Microsoft.EntityFrameworkCore;
using Stac;
using Stac.Api.Interfaces;
using Stac.Api.Services.Pagination;
using Stac.Api.WebApi.Services;

namespace Geopilot.Api.StacServices;

/// <summary>
/// Provides access to STAC items.
/// </summary>
public class StacItemsProvider : IItemsProvider
{
    private readonly ILogger<StacItemsProvider> logger;
    private readonly IDbContextFactory<Context> contextFactory;
    private readonly StacConverter stacConverter;
    private readonly IHttpContextAccessor httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="StacItemsProvider"/> class.
    /// </summary>
    public StacItemsProvider(ILogger<StacItemsProvider> logger, IDbContextFactory<Context> contextFactory, StacConverter stacConverter, IHttpContextAccessor httpContextAccessor)
    {
        this.logger = logger;
        this.contextFactory = contextFactory;
        this.stacConverter = stacConverter;
        this.httpContextAccessor = httpContextAccessor;
    }

    private bool IsAdmin => StacAudienceFilter.IsAdmin(httpContextAccessor.HttpContext);

    /// <inheritdoc/>
    public bool AnyItemsExist(IEnumerable<StacItem> items, IStacApiContext stacApiContext)
    {
        ArgumentNullException.ThrowIfNull(stacApiContext);

        var isAdmin = IsAdmin;
        using var db = contextFactory.CreateDbContext();
        foreach (var collection in stacApiContext.Collections)
        {
            try
            {
                return db.MandatesInStac(isAdmin).FirstOrDefault(m => stacConverter.GetCollectionId(m) == collection)?.Deliveries?.Count > 0;
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
            var isAdmin = IsAdmin;
            using var db = contextFactory.CreateDbContext();
            var delivery = db.DeliveriesInStac(isAdmin)
                .AsNoTracking()
                .AsEnumerable()
                .FirstOrDefault(d => stacConverter.GetItemId(d) == featureId && d.Mandate != null && stacConverter.GetCollectionId(d.Mandate) == stacApiContext.Collections.First())
                ?? throw new InvalidOperationException($"Item with id {featureId} does not exist.");

            // The catalog hides a delivery until it is forwarded, so the item of its successor must not give its time away.
            if (delivery.PrecursorDelivery is not null && db.DeliveryReactions.UnforwardedDeliveryIds().Contains(delivery.PrecursorDelivery.Id))
            {
                delivery.PrecursorDelivery = null;
            }

            var item = stacConverter.ToStacItem(delivery, isAdmin);
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

        FixPaginationParameters(stacApiContext);

        var items = new List<StacItem>();

        var collectionIds = stacApiContext.Collections?.ToList();
        var isAdmin = IsAdmin;
        using var db = contextFactory.CreateDbContext();
        IEnumerable<Mandate> mandates = await db.MandatesInStac(isAdmin).AsNoTracking().ToListAsync(cancellationToken);

        if (collectionIds?.Count > 0)
        {
            mandates = mandates.Where(m => collectionIds.Contains(stacConverter.GetCollectionId(m)));
        }

        foreach (var mandate in mandates)
        {
            items.AddRange(mandate.Deliveries.Select(d => stacConverter.ToStacItem(d, isAdmin)));
        }

        return items;
    }

    private void FixPaginationParameters(IStacApiContext stacApiContext)
    {
        // Fix limit defaulting to 0 instead of null, resulting in no items returned from search.
        switch (stacApiContext.Properties[PaginationExtensions.PaginationPropertiesKey])
        {
            case QueryStringPaginationParameters queryPagination when queryPagination.Limit == 0:
                queryPagination.Limit = null;
                break;
            case BodyPaginationParameters bodyPagination when bodyPagination.Limit == 0:
                bodyPagination.Limit = null;
                break;
        }
    }
}
