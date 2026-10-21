using Microsoft.EntityFrameworkCore;
using Stac;
using Stac.Api.Interfaces;
using Stac.Api.WebApi.Implementations.Default;

namespace Geopilot.Api.StacServices;

/// <summary>
/// Provides access to STAC collections.
/// </summary>
public class StacCollectionsProvider : ICollectionsProvider
{
    private readonly ILogger<StacCollectionsProvider> logger;
    private readonly IDbContextFactory<Context> contextFactory;
    private readonly StacConverter stacConverter;
    private readonly IHttpContextAccessor httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="StacCollectionsProvider"/> class.
    /// </summary>
    public StacCollectionsProvider(ILogger<StacCollectionsProvider> logger, IDbContextFactory<Context> contextFactory, StacConverter stacConverter, IHttpContextAccessor httpContextAccessor)
    {
        this.logger = logger;
        this.contextFactory = contextFactory;
        this.stacConverter = stacConverter;
        this.httpContextAccessor = httpContextAccessor;
    }

    private bool IsAdmin => StacAudienceFilter.IsAdmin(httpContextAccessor.HttpContext);

    /// <inheritdoc/>
    public Task<StacCollection> GetCollectionByIdAsync(string collectionId, IStacApiContext stacApiContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var isAdmin = IsAdmin;
            using var db = contextFactory.CreateDbContext();
            var mandate = db.MandatesWithIncludes
                .AsNoTracking()
                .VisibleInStac(isAdmin)
                .AsEnumerable()
                .FirstOrDefault(m => stacConverter.GetCollectionId(m) == collectionId)
                ?? throw new InvalidOperationException($"Collection with id {collectionId} does not exist.");
            var collection = stacConverter.ToStacCollection(mandate, isAdmin);
            return Task.FromResult(collection);
        }
        catch (Exception ex)
        {
            var message = $"Error while getting collection with id {collectionId}";
            logger.LogError(ex, message);
            throw new InvalidOperationException(message, ex);
        }
    }

    /// <inheritdoc/>
    public Task<IEnumerable<StacCollection>> GetCollectionsAsync(IStacApiContext stacApiContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stacApiContext);

        var isAdmin = IsAdmin;
        using var db = contextFactory.CreateDbContext();
        var collections = db.MandatesWithIncludes
            .AsNoTracking()
            .VisibleInStac(isAdmin)
            .ToList()
            .Select(m => stacConverter.ToStacCollection(m, isAdmin));
        stacApiContext.Properties.SetProperty(DefaultConventions.MatchedCountPropertiesKey, collections.Count());

        return Task.FromResult(collections);
    }
}
