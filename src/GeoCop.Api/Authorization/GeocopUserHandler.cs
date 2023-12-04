using Microsoft.AspNetCore.Authorization;

namespace GeoCop.Api.Authorization;

/// <summary>
/// Authorization handler for <see cref="GeocopUserRequirement"/>.
/// </summary>
public class GeocopUserHandler : AuthorizationHandler<GeocopUserRequirement>
{
    private readonly Context dbContext;
    private readonly Logger<GeocopUserHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeocopUserHandler"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger used for authorization related logging.</param>
    public GeocopUserHandler(Logger<GeocopUserHandler> logger, Context dbContext)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, GeocopUserRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var dbUser = await dbContext.GetUserByPrincipalAsync(context.User);
        if (dbUser == null)
        {
            logger.LogWarning("There was a logging attempt for user with id <{UserId}> without corresponding user in database.",
                context.User.Claims.FirstOrDefault(claim => claim.Type == "oid")?.Value.Replace(Environment.NewLine, string.Empty));
            return;
        }

        if (requirement.RequireAdmin && !dbUser.IsAdmin)
        {
            logger.LogWarning("User with id <{UserId}> did not fulfill admin requirement.", dbUser.AuthIdentifier);
            return;
        }

        context.Succeed(requirement);
    }
}
