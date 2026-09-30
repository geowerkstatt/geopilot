using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Authorization handler for <see cref="GeopilotUserRequirement"/>: the caller has to be an active user, and
/// an administrator where the requirement says so. Who the user is comes from <see cref="IGeopilotUserResolver"/>.
/// </summary>
public class GeopilotUserHandler : AuthorizationHandler<GeopilotUserRequirement>
{
    private readonly ILogger<GeopilotUserHandler> logger;
    private readonly IGeopilotUserResolver userResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopilotUserHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger used for authorization related logging.</param>
    /// <param name="userResolver">Resolves the current request to its user.</param>
    public GeopilotUserHandler(ILogger<GeopilotUserHandler> logger, IGeopilotUserResolver userResolver)
    {
        this.logger = logger;
        this.userResolver = userResolver;
    }

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, GeopilotUserRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var user = await userResolver.ResolveAsync();
        if (user is null)
            return;

        if (user.State == UserState.Inactive)
        {
            logger.LogWarning("User with id <{UserId}> is not active.", user.AuthIdentifier);
            context.Fail(new AuthorizationFailureReason(this, string.Format(CultureInfo.InvariantCulture, "User with id <{0}> is not active.", user.AuthIdentifier)));
            return;
        }

        if (requirement.RequireAdmin && !user.IsAdmin)
        {
            logger.LogWarning("User with id <{UserId}> did not fulfill admin requirement.", user.AuthIdentifier);
            return;
        }

        context.Succeed(requirement);
    }
}
