using Bogus;
using Bogus.DataSets;
using GeoCop.Api.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;

namespace GeoCop.Api;

internal static class ContextExtensions
{
    internal const string UserIdClaim = "oid";
    private const string NameClaim = "name";
    private const string EmailClaim = "email";

    /// <summary>
    /// Retreives the user that matches the provided principal from the database.
    /// Automatically updates the user information in the database if it has changed.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="principal">The user principal.</param>
    /// <returns>The matching <see cref="User"/> from the database or <c>null</c>.</returns>
    public static async Task<User?> GetUserByPrincipalAsync(this Context context, ClaimsPrincipal principal)
    {
        var userId = principal.Claims.FirstOrDefault(claim => claim.Type == UserIdClaim)?.Value;
        var name = principal.Claims.FirstOrDefault(claim => claim.Type == NameClaim)?.Value;
        var email = principal.Claims.FirstOrDefault(claim => claim.Type == EmailClaim)?.Value;
        if (userId == null || name == null || email == null)
        {
            return null;
        }

        var user = await context.Users.FirstOrDefaultAsync(u => u.AuthIdentifier == userId);
        if (user == null)
        {
            return null;
        }

        if (user.Email != email || user.FullName != name)
        {
            // Update user information in database from provided principal
            user.Email = email;
            user.FullName = name;
            await context.SaveChangesAsync();
        }

        return user;
    }

    public static void SeedTestData(this Context context)
    {
        var transaction = context.Database.BeginTransaction();

        // Set Bogus Data System Clock
        Date.SystemClock = () => DateTime.Parse("01.11.2023 00:00:00", new CultureInfo("de_CH", false));

        context.SeedUsers();
        context.SeedOrganisations();
        context.SeedOperate();
        context.SeedDeliveries();
        context.SeedAssets();
        context.AuthorizeFirstUser();

        transaction.Commit();
    }

    public static void SeedUsers(this Context context)
    {
        var userFaker = new Faker<User>()
            .StrictMode(true)
            .RuleFor(u => u.Id, _ => 0)
            .RuleFor(u => u.AuthIdentifier, f => f.Random.Uuid().ToString())
            .RuleFor(u => u.FullName, f => f.Person.FullName)
            .RuleFor(u => u.Email, f => f.Person.Email)
            .RuleFor(u => u.IsAdmin, f => f.IndexFaker == 0)
            .RuleFor(u => u.Organisations, _ => new List<Organisation>())
            .RuleFor(u => u.Deliveries, _ => new List<Delivery>());

        User SeedUsers(int seed) => userFaker.UseSeed(seed).Generate();
        context.Users.AddRange(Enumerable.Range(0, 10).Select(SeedUsers));
        context.SaveChanges();
    }

    public static void SeedOrganisations(this Context context)
    {
        var organisationFaker = new Faker<Organisation>()
            .StrictMode(true)
            .RuleFor(o => o.Id, _ => 0)
            .RuleFor(o => o.Name, f => f.Company.CompanyName())
            .RuleFor(o => o.Users, f => f.PickRandom(context.Users.ToList(), f.Random.Number(1, 4)).ToList())
            .RuleFor(o => o.Mandates, _ => new List<DeliveryMandate>());

        Organisation SeedOrganisations(int seed) => organisationFaker.UseSeed(seed).Generate();
        context.Organisations.AddRange(Enumerable.Range(0, 3).Select(SeedOrganisations));
        context.SaveChanges();
    }

    public static Geometry GetExtent(this Address address)
    {
        var longitude = address.Longitude();
        var latitude = address.Latitude();

        return GeometryFactory.Default.CreatePolygon(new Coordinate[]
        {
            new (longitude - 0.1, latitude - 0.1),
            new (longitude + 0.1, latitude - 0.1),
            new (longitude + 0.1, latitude + 0.1),
            new (longitude - 0.1, latitude + 0.1),
            new (longitude - 0.1, latitude - 0.1),
        });
    }

    public static void SeedOperate(this Context context)
    {
        var knownFileFormats = new string[] { ".xtf", ".gpkg", ".*", ".itf", ".xml", ".zip", ".csv" };
        var operateFaker = new Faker<DeliveryMandate>()
            .StrictMode(true)
            .RuleFor(o => o.Id, f => 0)
            .RuleFor(o => o.Name, f => f.Commerce.ProductName())
            .RuleFor(o => o.FileTypes, f => f.PickRandom(knownFileFormats, 4).Distinct().ToArray())
            .RuleFor(o => o.SpatialExtent, f => f.Address.GetExtent())
            .RuleFor(o => o.Organisations, f => f.PickRandom(context.Organisations.ToList(), 1).ToList())
            .RuleFor(o => o.Deliveries, _ => new List<Delivery>());

        DeliveryMandate SeedOperat(int seed) => operateFaker.UseSeed(seed).Generate();
        context.DeliveryMandates.AddRange(Enumerable.Range(0, 10).Select(SeedOperat));
        context.SaveChanges();
    }

    public static void SeedDeliveries(this Context context)
    {
        var deliveryContracts =
            context.DeliveryMandates
                .Include(o => o.Organisations)
                .ThenInclude(o => o.Users)
                .Where(o => o.Organisations.SelectMany(o => o.Users).Any())
                .ToList();

        var deliveryFaker = new Faker<Delivery>()
            .StrictMode(true)
            .RuleFor(d => d.Id, 0)
            .RuleFor(d => d.JobId, f => f.Random.Guid())
            .RuleFor(d => d.Date, f => f.Date.Past().ToUniversalTime())
            .RuleFor(d => d.DeliveryMandate, f => f.PickRandom(deliveryContracts))
            .RuleFor(d => d.DeclaringUser, (f, d) => f.PickRandom(
                d.DeliveryMandate.Organisations
                .SelectMany(o => o.Users)
                .ToList()))
            .RuleFor(d => d.Assets, _ => new List<Asset>());

        Delivery SeedDelivery(int seed) => deliveryFaker.UseSeed(seed).Generate();
        context.Deliveries.AddRange(Enumerable.Range(0, 20).Select(SeedDelivery));
        context.SaveChanges();
    }

    public static void SeedAssets(this Context context)
    {
        var assetFaker = new Faker<Asset>()
            .StrictMode(true)
            .RuleFor(a => a.Id, _ => 0)
            .RuleFor(a => a.FileHash, f => SHA256.HashData(f.Random.Bytes(10)))
            .RuleFor(a => a.OriginalFilename, f => f.System.FileName())
            .RuleFor(a => a.SanitizedFilename, (f, a) => Path.ChangeExtension(Path.GetRandomFileName(), Path.GetExtension(a.OriginalFilename)))
            .RuleFor(a => a.AssetType, f => f.PickRandomWithout(AssetType.PrimaryData))
            .RuleFor(a => a.Delivery, f => f.PickRandom(context.Deliveries.ToList()));

        Asset SeedAsset(int seed) => assetFaker.UseSeed(seed).Generate();
        var assets = Enumerable.Range(0, 60).Select(SeedAsset).ToList();

        foreach (var assetGroup in assets.GroupBy(a => a.Delivery))
        {
            var firstAsset = assetGroup.FirstOrDefault();
            if (firstAsset is not null)
                firstAsset.AssetType = AssetType.PrimaryData;
        }

        context.Assets.AddRange(assets);
        context.SaveChanges();
    }

    public static void AuthorizeFirstUser(this Context context)
    {
        var user = context.Users.OrderBy(u => u.Id).First();
        var organisation = context.Organisations.OrderBy(o => o.Id).First();
        var deliveryMandates = context.DeliveryMandates.OrderBy(m => m.Id).Skip(1);

        user.IsAdmin = true;
        user.Organisations.Add(organisation);
        organisation.Mandates.AddRange(deliveryMandates);

        context.SaveChanges();
    }
}
