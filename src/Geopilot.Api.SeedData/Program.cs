using Geopilot.Api;
using Geopilot.Api.SeedData;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

var connectionString = args.FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Context")
    ?? "Server=localhost;Port=5432;Database=geopilot;User Id=HAPPYWALK;Password=SOMBERSPORK;";

var options = new DbContextOptionsBuilder<Context>()
    .UseNpgsql(connectionString, o =>
    {
        o.UseNetTopologySuite();
        o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    })
    .Options;

using var context = new Context(options);

// Generous, because this runs as a container that starts before Visual Studio has built and started
// the API on the host. It returns as soon as the migrations are applied, so a long window costs nothing.
if (!await WaitForSchemaAsync(context, TimeSpan.FromMinutes(10)))
{
    Console.WriteLine("Seed skipped: the database schema was not ready in time.");
    return 1;
}

// Signing in creates a user row, and the seed data contains those same accounts. Seeding on top of
// them would produce duplicates, so only an untouched database is seeded.
if (context.Users.Any() || context.Mandates.Any())
{
    Console.WriteLine("Seed skipped: the database already contains data. Recreate the database volume to seed again.");
    return 0;
}

context.SeedTestData();
Console.WriteLine("Test data created.");
return 0;

// The API applies the migrations on startup. This tool may start at the same time, so it waits for
// the schema to be complete instead of migrating itself and racing the API for the same work.
static async Task<bool> WaitForSchemaAsync(Context context, TimeSpan timeout)
{
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
        if (await IsSchemaReadyAsync(context))
        {
            return true;
        }

        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    return false;
}

// A database that is starting up, restarting or briefly unreachable is the state this waits for, not
// an error: only the timeout decides when to give up.
static async Task<bool> IsSchemaReadyAsync(Context context)
{
    try
    {
        return await context.Database.CanConnectAsync()
            && !(await context.Database.GetPendingMigrationsAsync()).Any();
    }
    catch (DbException)
    {
        return false;
    }
}
