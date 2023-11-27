using Microsoft.AspNetCore.Authorization;

namespace GeoCop.Api.Authorization;

/// <summary>
/// Authorization handler for <see cref="GeocopUserRequirement"/>.
/// </summary>
public class GeocopUserHandler : AuthorizationHandler<GeocopUserRequirement>
{
    private readonly Context dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeocopUserHandler"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public GeocopUserHandler(Context dbContext)
    {
        this.dbContext = dbContext;
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
            return;
        }

        if (!requirement.RequireAdmin || dbUser.IsAdmin)
        {
            context.Succeed(requirement);
        }
    }
}
