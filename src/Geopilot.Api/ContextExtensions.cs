using Geopilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Geopilot.Api;

internal static class ContextExtensions
{
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

    /// <summary>
    /// Filters mandates to those that accept all files or the provided file extension.
    /// </summary>
    public static IQueryable<Mandate> FilterMandatesByFileExtensions(this IQueryable<Mandate> mandates, IEnumerable<string> fileExtensions)
    {
        var extensions = fileExtensions.Select(f => f.ToLowerInvariant()).ToHashSet();
        #pragma warning disable CA1304, CA1311 // Specify a culture or use an invariant version
        return mandates.Where(m => m.FileTypes.Any(ft => ft == ".*" || extensions.Contains(ft.ToLower())));
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
