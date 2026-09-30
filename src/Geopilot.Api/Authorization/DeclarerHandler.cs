using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Authorization handler for <see cref="DeclarerRequirement"/>. A registered machine client is admitted on
/// its subject alone, without asking the identity provider anything: its token carries no person and most
/// providers describe none for it. Everyone else is admitted the way the user policy admits them. The two
/// branches are kept apart on purpose, so that restricting the machine delivery to machines later is the
/// removal of one of them.
/// </summary>
public class DeclarerHandler : AuthorizationHandler<DeclarerRequirement>
{
    private readonly ILogger<DeclarerHandler> logger;
    private readonly Context dbContext;
    private readonly IGeopilotUserResolver userResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeclarerHandler"/> class.
    /// </summary>
    public DeclarerHandler(ILogger<DeclarerHandler> logger, Context dbContext, IGeopilotUserResolver userResolver)
    {
        this.logger = logger;
        this.dbContext = dbContext;
        this.userResolver = userResolver;
    }

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, DeclarerRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var client = subject is null
            ? null
            : await dbContext.MachineClients.AsNoTracking().SingleOrDefaultAsync(c => c.AuthIdentifier == subject);

        if (client is not null)
        {
            if (client.State == MachineClientState.Inactive)
            {
                logger.LogWarning("Machine client <{ClientId}> is not active.", client.Id);
                context.Fail(new AuthorizationFailureReason(this, string.Format(CultureInfo.InvariantCulture, "Machine client <{0}> is not active.", client.Id)));
                return;
            }

            context.Succeed(requirement);
            return;
        }

        var user = await userResolver.ResolveAsync();
        if (user is null)
        {
            // The one place a machine learns that nobody knows it. The subject is what an administrator has to
            // register, so it goes into the log rather than the caller having to find it at the identity provider.
            logger.LogWarning("Subject <{Sub}> is neither a user nor a registered machine client.", subject);
            return;
        }

        if (user.State == UserState.Inactive)
        {
            logger.LogWarning("User with id <{UserId}> is not active.", user.AuthIdentifier);
            context.Fail(new AuthorizationFailureReason(this, string.Format(CultureInfo.InvariantCulture, "User with id <{0}> is not active.", user.AuthIdentifier)));
            return;
        }

        context.Succeed(requirement);
    }
}
