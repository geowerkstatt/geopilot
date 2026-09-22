using Geopilot.Api.Models;
using Geopilot.Api.Services;
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
    /// Retrieves the user that matches the provided principal, or <see langword="null"/> when the principal is
    /// no user: a machine client, or a token the application has never seen as a person.
    /// </summary>
    public static async Task<User?> GetUserOrDefaultByPrincipalAsync(this Context context, ClaimsPrincipal principal)
    {
        var subjectId = principal.Claims.First(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value;
        return await context.Users.SingleOrDefaultAsync(u => u.AuthIdentifier == subjectId);
    }

    /// <summary>
    /// Resolves the provided principal to whoever it declares deliveries as: the machine client registered
    /// under its subject, or failing that the user with that subject. The client comes first because a
    /// registered client is never a user, however the subject got a user row before it was registered.
    /// </summary>
    /// <exception cref="InvalidOperationException">The subject is neither a machine client nor a user. The
    /// <see cref="Authorization.GeopilotPolicies.Declarer"/> policy rules that out before an action runs.</exception>
    public static async Task<Declarer> GetDeclarerAsync(this Context context, ClaimsPrincipal principal)
    {
        return await context.GetDeclarerOrDefaultAsync(principal)
            ?? throw new InvalidOperationException($"Subject <{GetSubject(principal)}> is neither an active machine client nor a user.");
    }

    /// <summary>
    /// Resolves the provided principal like <see cref="GetDeclarerAsync"/>, but answers <see langword="null"/>
    /// for a subject that is neither an active machine client nor a user, for the anonymous endpoints, where
    /// such a caller is simply nobody in particular.
    /// </summary>
    public static async Task<Declarer?> GetDeclarerOrDefaultAsync(this Context context, ClaimsPrincipal principal)
    {
        var subjectId = GetSubject(principal);

        // A registered subject is its client or nobody, never a person: a deactivated client must not fall
        // through to the user lookup, or revoking it would hand it the rights of a user with the same subject.
        var client = await context.MachineClients
            .Where(c => c.AuthIdentifier == subjectId)
            .Select(c => new { c.Id, c.State })
            .SingleOrDefaultAsync();
        if (client is not null)
            return client.State == MachineClientState.Active ? Declarer.ForClient(client.Id) : null;

        var userId = await context.Users
            .Where(u => u.AuthIdentifier == subjectId)
            .Select(u => (int?)u.Id)
            .SingleOrDefaultAsync();
        if (userId is not null)
            return Declarer.ForUser(userId.Value);

        return null;
    }

    private static string GetSubject(ClaimsPrincipal principal) =>
        principal.Claims.First(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value;

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
