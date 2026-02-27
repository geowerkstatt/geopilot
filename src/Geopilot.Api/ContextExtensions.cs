using Bogus;
using Geopilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Geopilot.Api;

internal static class ContextExtensions
{
    private static readonly double[] extentCh = new double[] { 7.536621, 46.521076, 9.398804, 47.476376 };
    private static readonly DateTime referenceDateTime = DateTime.Parse("01.11.2023 00:00:00", new CultureInfo("de_CH", false));

    /// <summary>
    /// Retreives the user that matches the provided principal from the database.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="principal">The user principal.</param>
    /// <returns>The matching <see cref="User"/> from the database.</returns>
    public static async Task<User> GetUserByPrincipalAsync(this Context context, ClaimsPrincipal principal)
    {
        var subjectId = principal.Claims.First(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value;
        return await context.Users.SingleAsync(u => u.AuthIdentifier == subjectId);
    }

    public static void SeedTestData(this Context context)
    {
        var transaction = context.Database.BeginTransaction();

        context.SeedUsers();
        context.SeedOrganisations();
        context.SeedMandates();
        context.SeedDeliveries();
        context.SeedAssets();
        context.AddOrganisationsToDefaultUsers();

        transaction.Commit();
    }

    public static void SeedUsers(this Context context)
    {
        var userFaker = new Faker<User>()
            .UseDateTimeReference(referenceDateTime)
            .StrictMode(true)
            .RuleFor(u => u.Id, _ => 0)
            .RuleFor(u => u.AuthIdentifier, f => f.Random.Uuid().ToString())
            .RuleFor(u => u.FullName, f => f.Person.FullName)
            .RuleFor(u => u.Email, f => f.Person.Email)
            .RuleFor(u => u.IsAdmin, f => f.IndexFaker == 0)
            .RuleFor(u => u.State, f => UserState.Active)
            .RuleFor(u => u.Organisations, _ => new List<Organisation>())
            .RuleFor(u => u.Deliveries, _ => new List<Delivery>());

        User SeedUsers(int seed) => userFaker.UseSeed(seed).Generate();
        context.Users.AddRange(Enumerable.Range(0, 10).Select(SeedUsers));
        context.Users.Add(new User
        {
            AuthIdentifier = "1f9f9000-c651-4b04-b6ae-9ce1e7f45c15",
            FullName = "Andreas Admin",
            Email = "admin@geopilot.ch",
            IsAdmin = true,
            State = UserState.Active,
        });

        context.Users.Add(new User
        {
            AuthIdentifier = "1ed45832-2880-4fd4-a274-bbcc101c3307",
            FullName = "Ursula User",
            Email = "user@geopilot.ch",
            IsAdmin = false,
            State = UserState.Active,
        });

        context.SaveChanges();
    }

    public static void SeedOrganisations(this Context context)
    {
        var organisationFaker = new Faker<Organisation>()
            .UseDateTimeReference(referenceDateTime)
            .StrictMode(true)
            .RuleFor(o => o.Id, _ => 0)
            .RuleFor(o => o.Name, f => f.Company.CompanyName())
            .RuleFor(o => o.Users, f => f.PickRandom(context.Users.ToList(), f.Random.Number(1, 4)).ToList())
            .RuleFor(o => o.Mandates, _ => new List<Mandate>());

        Organisation SeedOrganisations(int seed) => organisationFaker.UseSeed(seed).Generate();
        context.Organisations.AddRange(Enumerable.Range(0, 3).Select(SeedOrganisations));
        context.SaveChanges();
    }

    public static Geometry GetExtent()
    {
        var longDiffHalf = (extentCh[2] - extentCh[0]) / 2;
        var latDiffHalf = (extentCh[3] - extentCh[1]) / 2;

        var longMin = new Faker().Random.Double(extentCh[0], extentCh[0] + longDiffHalf);
        var latMin = new Faker().Random.Double(extentCh[1], extentCh[1] + latDiffHalf);
        var longMax = new Faker().Random.Double(extentCh[2] - longDiffHalf, extentCh[2]);
        var latMax = new Faker().Random.Double(extentCh[3] - latDiffHalf, extentCh[3]);

        return GeometryFactory.Default.CreatePolygon(new NetTopologySuite.Geometries.Coordinate[]
        {
            new(longMin, latMin),
            new(longMax, latMin),
            new(longMax, latMax),
            new(longMin, latMax),
            new(longMin, latMin),
        });
    }

    public static void SeedMandates(this Context context)
    {
        var knownFileFormats = new string[] { ".xtf", ".gpkg", ".*", ".itf", ".xml", ".zip", ".csv" };
        var mandateFaker = new Faker<Mandate>()
            .UseDateTimeReference(referenceDateTime)
            .StrictMode(true)
            .RuleFor(o => o.Id, f => 0)
            .RuleFor(o => o.Name, f => f.Commerce.ProductName())
            .RuleFor(o => o.FileTypes, f => f.PickRandom(knownFileFormats, 4).Distinct().ToArray())
            .RuleFor(o => o.PipelineId, f => "ili_validation")
            .RuleFor(o => o.EvaluatePrecursorDelivery, f => f.PickRandom<FieldEvaluationType>())
            .RuleFor(o => o.EvaluatePartial, f => f.PickRandom(FieldEvaluationType.NotEvaluated, FieldEvaluationType.Required))
            .RuleFor(o => o.EvaluateComment, f => f.PickRandom<FieldEvaluationType>())
            .RuleFor(o => o.SpatialExtent, f => GetExtent())
            .Ignore(o => o.Coordinates)
            .RuleFor(o => o.Organisations, f => f.PickRandom(context.Organisations.ToList(), 1).ToList())
            .RuleFor(o => o.Deliveries, _ => new List<Delivery>())
            .RuleFor(o => o.IsPublic, f => false)
            .RuleFor(o => o.AllowDelivery, f => true);

        Mandate SeedMandate(int seed) => mandateFaker.UseSeed(seed).Generate();
        context.Mandates.AddRange(Enumerable.Range(0, 9).Select(SeedMandate));

        context.Mandates.Add(new Mandate()
        {
            Name = "Public Mandate",
            PipelineId = "ili_validation",
            FileTypes = [".xtf"],
            SpatialExtent = GetExtent(),
            IsPublic = true,
            AllowDelivery = true,
        });

        context.SaveChanges();
    }

    public static void SeedDeliveries(this Context context)
    {
        var deliveryContracts =
            context.Mandates
                .Include(o => o.Organisations)
                .ThenInclude(o => o.Users)
                .Where(o => o.Organisations.SelectMany(o => o.Users).Any())
                .ToList();

        var deliveryFaker = new Faker<Delivery>()
            .UseDateTimeReference(referenceDateTime)
            .StrictMode(true)
            .RuleFor(d => d.Id, 0)
            .RuleFor(d => d.JobId, f => f.Random.Guid())
            .RuleFor(d => d.Date, f => f.Date.Past().ToUniversalTime())
            .RuleFor(d => d.Mandate, f => f.PickRandom(deliveryContracts))
            .RuleFor(d => d.DeclaringUser, (f, d) => f.PickRandom(
                d.Mandate!.Organisations
                .SelectMany(o => o.Users)
                .ToList()))
            .RuleFor(d => d.Assets, _ => new List<Asset>())
            .RuleFor(d => d.Partial, f => f.Random.Bool())
            .RuleFor(d => d.PrecursorDelivery, f => f.PickRandom(context.Deliveries.ToList().Append(null)))
            .RuleFor(d => d.Comment, f => f.Rant.Review())
            .RuleFor(d => d.Deleted, false);

        Delivery SeedDelivery(int seed) => deliveryFaker.UseSeed(seed).Generate();
        for (int i = 0; i < 20; i++)
        {
            context.Deliveries.Add(SeedDelivery(i));
            context.SaveChanges();
        }
    }

    public static void SeedAssets(this Context context)
    {
        var assetFaker = new Faker<Asset>()
            .UseDateTimeReference(referenceDateTime)
            .StrictMode(true)
            .RuleFor(a => a.Id, _ => 0)
            .RuleFor(a => a.FileHash, f => SHA256.HashData(f.Random.Bytes(10)))
            .RuleFor(a => a.OriginalFilename, f => f.System.FileName())
            .RuleFor(a => a.SanitizedFilename, (f, a) => Path.ChangeExtension(Path.GetRandomFileName(), Path.GetExtension(a.OriginalFilename)))
            .RuleFor(a => a.AssetType, f => f.PickRandomWithout(AssetType.PrimaryData))
            .RuleFor(a => a.Delivery, f => f.PickRandom(context.Deliveries.ToList()))
            .RuleFor(d => d.Deleted, false);

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

    public static void AddOrganisationsToDefaultUsers(this Context context)
    {
        var admin = context.Users.Single(user => user.Email == "admin@geopilot.ch");
        var adminOrganisations = context.Organisations.OrderBy(o => o.Id).Skip(1);
        admin.Organisations.AddRange(adminOrganisations);

        var user = context.Users.Single(user => user.Email == "user@geopilot.ch");
        var userOrganistions = context.Organisations.OrderBy(o => o.Id).Take(2);
        user.Organisations.AddRange(userOrganistions);

        context.SaveChanges();
    }

    /// <summary>
    /// Filters mandates to those that accept all files or the provided file extension.
    /// </summary>
    public static IQueryable<Mandate> FilterMandatesByFileExtension(this IQueryable<Mandate> mandates, string fileExtension)
    {
        var extension = fileExtension.ToLowerInvariant();

        // EF Core can only translate ToLower without culture info.
#pragma warning disable CA1304, CA1311 // Specify a culture or use an invariant version
        return mandates
            .Where(m => m.FileTypes.Contains(".*") || m.FileTypes.Select(ft => ft.ToLower()).Contains(extension));
#pragma warning restore CA1304, CA1311 // Specify a culture or use an invariant version
    }

    public static void MigrateDatabase(this Context context)
    {
        if (context.IsPostgisInstalled())
        {
            // Workaround for azure dbs requiring admin permission for "CREATE EXTENSION IF NOT EXISTS postgis" even if it is already installed.
            // See: https://github.com/npgsql/efcore.pg/issues/3496
            var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
            var migrationScript = migrator
                .GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent)
                .Replace("CREATE EXTENSION IF NOT EXISTS postgis;", "");
            context.Database.ExecuteSqlRaw(migrationScript);
        }
        else
        {
            // Full migration including postgis installation.
            context.Database.Migrate();
        }
    }

    private static bool IsPostgisInstalled(this Context context)
    {
        return context.Database
            .SqlQuery<int>($"SELECT 1 FROM pg_extension WHERE extname = 'postgis'")
            .Any();
    }
}
