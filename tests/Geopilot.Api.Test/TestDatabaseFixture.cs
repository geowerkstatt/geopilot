using Geopilot.Api.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Geopilot.Api;

public class TestDatabaseFixture
{
    internal const string ConnectionString = "Server=localhost;Port=5432;Database=geopilot-test;User Id=HAPPYWALK;Password=SOMBERSPORK;";

    private static readonly object @lock = new();
    private static bool databaseInitialized;

    public TestDatabaseFixture()
    {
        lock (@lock)
        {
            if (!databaseInitialized)
            {
                using var context = CreateContext();

                context.Database.EnsureDeleted();

                // Migrate instead of EnsureCreated so the schema matches what Program.cs expects.
                context.Database.Migrate();

                context.SeedTestData();
                context.SaveChanges();

                databaseInitialized = true;
            }
        }
    }

    /// <summary>
    /// Creates a new Context and starts a transaction.
    /// When disposing the context, the transaction will be rolled back.
    /// Use context.ChangeTracker.Clear() to clear the context before
    /// data should be loaded explicitly from the database.
    /// </summary>
    /// <returns>Context in transactional state.</returns>
    public Context GetTestContext() => GetTestContext([]);

    /// <summary>
    /// Like <see cref="GetTestContext()"/>, with interceptors on the context, e.g. one that makes a write fail.
    /// </summary>
    /// <param name="interceptors">The interceptors of the context.</param>
    /// <returns>Context in transactional state.</returns>
    public Context GetTestContext(params IInterceptor[] interceptors)
    {
        var context = CreateContext(interceptors);
        context.Database.BeginTransaction();
        return context;
    }

    private Context CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<Context>()
            .UseNpgsql(ConnectionString, o =>
            {
                o.UseNetTopologySuite();
                o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .AddInterceptors(interceptors)
            .Options;

        return new Context(options);
    }
}
