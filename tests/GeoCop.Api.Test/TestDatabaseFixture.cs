using Microsoft.EntityFrameworkCore;

namespace GeoCop.Api.Test
{
    public class TestDatabaseFixture
    {
        private const string ConnectionString = "Server=localhost;Port=5432;Database=geocop-test;User Id=HAPPYWALK;Password=SOMBERSPORK;";

        private static readonly object @lock = new ();
        private static bool databaseInitialized;

        public TestDatabaseFixture()
        {
            lock (@lock)
            {
                if (!databaseInitialized)
                {
                    using var context = CreateContext();

                    context.Database.EnsureDeleted();
                    context.Database.EnsureCreated();

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
        public Context GetTestContext()
        {
            var context = CreateContext();
            context.Database.BeginTransaction();
            return context;
        }

        private Context CreateContext()
        {
            var options = new DbContextOptionsBuilder<Context>()
                .UseNpgsql(ConnectionString, o =>
                {
                    o.UseNetTopologySuite();
                    o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
                .Options;

            return new Context(options);
        }
    }
}
