using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Web;
using Microsoft.Extensions.Primitives;
using Stac;
using Stac.Api.Clients.Collections;
using Stac.Api.Interfaces;
using Stac.Api.Models;
using Stac.Api.Models.Core;
using Stac.Api.WebApi.Services;

namespace GeoCop.Api.StacServices;

/// <summary>
/// Custom linker for STAC. Based on https://github.com/Terradue/DotNetStac.Api/blob/main/src/Stac.Api.WebApi/Implementations/Default/CollectionBasedStacLinker.cs,
/// with some modifications to return correct 'self' links for collections.
/// </summary>
public class StacLinker : IStacLinker
{
    /// <inheritdoc/>
    public void Link(LandingPage landingPage, IStacApiContext stacApiContext)
    {
        var uri = stacApiContext.LinkGenerator.GetUriByAction(stacApiContext.HttpContext, "GetLandingPage", "Core");
        if (uri == null) throw new InvalidOperationException("Could not generate URL for action GetLandingPage on controller Core.");
        landingPage.Links.Add(StacLink.CreateSelfLink(new Uri(uri), "application/json"));
        landingPage.Links.Add(StacLink.CreateRootLink(new Uri(uri), "application/json"));
    }

    /// <inheritdoc/>
    public void Link(StacCollection collection, IStacApiContext stacApiContext)
    {
        collection.Links.Add(GetSelfLink(collection, stacApiContext));
        collection.Links.Add(GetRootLink(stacApiContext));
    }

    /// <inheritdoc/>
    public void Link(StacCollections collections, IStacApiContext stacApiContext)
    {
        collections.Links.Add(GetSelfLink(collections, stacApiContext));
        collections.Links.Add(GetRootLink(stacApiContext));
        foreach (StacCollection collection in collections.Collections)
        {
            Link(collection, stacApiContext);
        }
    }

    /// <inheritdoc/>
    public void Link(Stac.StacItem item, IStacApiContext stacApiContext)
    {
        item.Links.Add(GetSelfLink(item, stacApiContext));
        item.Links.Add(GetRootLink(stacApiContext));
    }

    /// <inheritdoc/>
    public void Link(StacFeatureCollection collection, IStacApiContext stacApiContext)
    {
        collection.Links.Add(GetSelfLink(collection, stacApiContext));
        collection.Links.Add(GetRootLink(stacApiContext));
        collection.Links.Add(GetParentLink(collection, stacApiContext));
        AddAdditionalLinks(collection, stacApiContext);
    }

    private StacApiLink GetRootLink(IStacApiContext stacApiContext)
    {
        return new StacApiLink(GetUriByAction(stacApiContext, "GetLandingPage", "Core", new { }, null), "root", null, "application/json");
    }

    /// <summary>
    /// Create self link for collections.
    /// </summary>
    /// <param name="stacCollections">The <see cref="StacCollections"/> for which to create the link.</param>
    /// <param name="stacApiContext">The <see cref="IStacApiContext"/> to build the link with.</param>
    /// <returns>A <see cref="StacApiLink"/> with relationshipType 'self'.</returns>
    protected StacApiLink GetSelfLink(StacCollections stacCollections, IStacApiContext stacApiContext)
    {
        return new StacApiLink(GetUriByAction(stacApiContext, "GetCollections", "Collections", new { }, null), "self", "Collections", "application/json");
    }

    /// <summary>
    /// Create self link for items.
    /// </summary>
    /// <param name="stacItem">The <see cref="Stac.StacItem"/> for which to create the link.</param>
    /// <param name="stacApiContext">The <see cref="IStacApiContext"/> to build the link with.</param>
    /// <returns>A <see cref="StacApiLink"/> with relationshipType 'self'.</returns>
    protected StacApiLink GetSelfLink(Stac.StacItem stacItem, IStacApiContext stacApiContext)
    {
        return new StacApiLink(
            GetUriByAction(
                stacApiContext,
                "GetFeature",
                "Features",
                new
                {
                    collectionId = stacItem.Collection,
                    featureId = stacItem.Id,
                },
                null),
            "self",
            stacItem.Title,
            stacItem.MediaType.ToString());
    }

    /// <summary>
    /// Create self link for collection.
    /// </summary>
    /// <param name="collection">The <see cref="StacCollection"/> for which to create the link.</param>
    /// <param name="stacApiContext">The <see cref="IStacApiContext"/> to build the link with.</param>
    /// <returns>A <see cref="StacApiLink"/> with relationshipType 'self'.</returns>
    protected StacApiLink GetSelfLink(StacCollection collection, IStacApiContext stacApiContext)
    {
        var link = new StacApiLink(GetUriByAction(
            stacApiContext,
            "GetCollections",
            "Collections",
            new
            {
                collectionId = collection.Id,
            },
            null),
            "self",
            collection.Title,
            collection.MediaType.ToString());
        link.Uri = new Uri(link.Uri.ToString().Replace("?collectionId=", "/"));
        return link;
    }

    /// <summary>
    /// Create self link for feature collection.
    /// </summary>
    /// <param name="collection">The <see cref="StacFeatureCollection"/> for which to create the link.</param>
    /// <param name="stacApiContext">The <see cref="IStacApiContext"/> to build the link with.</param>
    /// <returns>A <see cref="StacApiLink"/> with relationshipType 'self'.</returns>
    protected StacApiLink GetSelfLink(StacFeatureCollection collection, IStacApiContext stacApiContext)
    {
        var uri = stacApiContext.LinkGenerator.GetUriByRouteValues(stacApiContext.HttpContext, null, stacApiContext.HttpContext.Request.Query.ToDictionary((KeyValuePair<string, StringValues> x) => x.Key, (KeyValuePair<string, StringValues> x) => x.Value.ToString()));
        if (uri == null) throw new InvalidOperationException("Could not generate URL for action GetFeatureCollection on controller Features.");
        return new StacApiLink(new Uri(uri), "self", null, "application/geo+json");
    }

    private StacLink GetParentLink(StacFeatureCollection collection, IStacApiContext stacApiContext)
    {
        IList<string> collections = stacApiContext.Collections;
        if (collections != null && collections.Count == 1)
        {
            return new StacApiLink(
                GetUriByAction(
                    stacApiContext,
                    "DescribeCollection",
                    "Collections",
                    new
                    {
                        collectionId = stacApiContext.Collections.First(),
                    },
                    null),
                "parent",
                null,
                "application/json");
        }

        return new StacApiLink(GetUriByAction(stacApiContext, "GetCollections", "Collections", new { }, null), "parent", null, "application/json");
    }

    private Uri GetUriByAction(IStacApiContext stacApiContext, string actionName, string controllerName, object? value, IDictionary<string, object>? queryValues)
    {
        string? uriByAction = stacApiContext.LinkGenerator.GetUriByAction(stacApiContext.HttpContext, actionName, controllerName, value);
        if (uriByAction == null)
        {
            DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(61, 3);
            defaultInterpolatedStringHandler.AppendLiteral("Could not generate URL for action ");
            defaultInterpolatedStringHandler.AppendFormatted(actionName);
            defaultInterpolatedStringHandler.AppendLiteral(" on controller ");
            defaultInterpolatedStringHandler.AppendFormatted(controllerName);
            defaultInterpolatedStringHandler.AppendLiteral(" with value ");
            if (value != null) defaultInterpolatedStringHandler.AppendFormatted<object>(value);
            throw new InvalidOperationException(defaultInterpolatedStringHandler.ToStringAndClear());
        }

        UriBuilder uriBuilder = new UriBuilder(uriByAction);
        NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(uriBuilder.Query);
        foreach (KeyValuePair<string, object> item in (queryValues ?? new Dictionary<string, object>()) !)
        {
            nameValueCollection[item.Key] = item.Value.ToString();
        }

        uriBuilder.Query = nameValueCollection.ToString();
        return new Uri(uriBuilder.ToString());
    }

    internal static void AddAdditionalLinks(ILinksCollectionObject linksCollectionObject, IStacApiContext stacApiContext)
    {
        foreach (ILinkValues linkValue in stacApiContext.LinkValues)
        {
            StacApiLink item = CreateStacApiLink(stacApiContext, linkValue);
            linksCollectionObject.Links.Add(item);
        }
    }

    private static StacApiLink CreateStacApiLink(IStacApiContext stacApiContext, ILinkValues linkValue)
    {
        var uriByAction = stacApiContext.LinkGenerator.GetUriByAction(stacApiContext.HttpContext, linkValue.ActionName, linkValue.ControllerName);
        if (uriByAction == null) throw new InvalidOperationException($"Could not generate URL for action {linkValue.ActionName} on controller {linkValue.ControllerName}.");
        UriBuilder uriBuilder = new UriBuilder(uriByAction);
        NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(uriBuilder.Query);
        foreach (KeyValuePair<string, object> item in linkValue.QueryValues ?? new Dictionary<string, object>())
        {
            nameValueCollection[item.Key] = item.Value.ToString();
        }

        uriBuilder.Query = nameValueCollection.ToString();
        return new StacApiLink(new Uri(uriBuilder.ToString()), linkValue.RelationshipType.GetEnumMemberValue(), linkValue.Title, linkValue.MediaType);
    }

    /// <summary>
    /// Get self link for a STAC object.
    /// </summary>
    /// <param name="stacObject">The <see cref="IStacObject"/> for which to create the link.</param>
    /// <param name="stacApiContext">The <see cref="IStacApiContext"/> to build the link with.</param>
    /// <returns>A <see cref="StacApiLink"/> with relationshipType 'self'.</returns>
    /// <exception cref="InvalidOperationException">Exception if self link cannot be retrieved.</exception>
    public StacApiLink GetSelfLink(IStacObject stacObject, IStacApiContext stacApiContext)
    {
        Stac.StacItem? stacItem = stacObject as Stac.StacItem;
        if (stacItem != null)
        {
            return GetSelfLink(stacItem, stacApiContext);
        }

        StacCollection? stacCollection = stacObject as StacCollection;
        if (stacCollection != null)
        {
            return GetSelfLink(stacCollection, stacApiContext);
        }

        StacFeatureCollection? stacFeatureCollection = stacObject as StacFeatureCollection;
        if (stacFeatureCollection != null)
        {
            return GetSelfLink(stacFeatureCollection, stacApiContext);
        }

        StacCollections? stacCollections = stacObject as StacCollections;
        if (stacCollections != null)
        {
            return GetSelfLink(stacCollections, stacApiContext);
        }

        throw new InvalidOperationException("Cannot get self link for " + stacObject.GetType().Name);
    }
}
