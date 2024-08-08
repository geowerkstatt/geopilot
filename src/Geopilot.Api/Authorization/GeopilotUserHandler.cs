using Microsoft.AspNetCore.Authorization;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Authorization handler for <see cref="GeopilotUserRequirement"/>.
/// </summary>
public class GeopilotUserHandler : AuthorizationHandler<GeopilotUserRequirement>
{
    private readonly Context dbContext;
    private readonly ILogger<GeopilotUserHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopilotUserHandler"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger used for authorization related logging.</param>
    public GeopilotUserHandler(ILogger<GeopilotUserHandler> logger, Context dbContext)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, GeopilotUserRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var dbUser = await dbContext.GetUserByPrincipalAsync(context.User);
        if (dbUser == null)
        {
            logger.LogWarning("There was a logging attempt for user with id <{UserId}> without corresponding user in database.",
                context.User.Claims.FirstOrDefault(claim => claim.Type == ContextExtensions.UserIdClaim)?.Value.ReplaceLineEndings(string.Empty));
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
