using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

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
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var user = await UpdateOrCreateUser(context);
        if (user is null)
            return;

        if (requirement.RequireAdmin && !user.IsAdmin)
        {
            logger.LogWarning("User with id <{UserId}> did not fulfill admin requirement.", user.AuthIdentifier);
            return;
        }

        context.Succeed(requirement);
    }

    internal async Task<User?> UpdateOrCreateUser(AuthorizationHandlerContext context)
    {
        var sub = context.User.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Sub)?.Value;
        var email = context.User.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Email)?.Value;
        var name = context.User.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Name)?.Value;

        if (sub == null || name == null || email == null)
        {
            logger.LogError("Login failed as not all required claims were provided.");
            return null;
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.AuthIdentifier == sub);
        if (user == null)
        {
            user = new User { AuthIdentifier = sub, Email = email, FullName = name };

            // Elevate first user to admin
            if (!dbContext.Users.Any())
            {
                user.IsAdmin = true;
            }

            await dbContext.Users.AddAsync(user);
            logger.LogInformation("New user (with sub <{Sub}>) has been registered in database.", sub);
        }
        else if (user.Email != email || user.FullName != name)
        {
            // Update user information in database from provided principal
            user.Email = email;
            user.FullName = name;
        }

        await dbContext.SaveChangesAsync();

        return await dbContext.Users.SingleAsync(u => u.AuthIdentifier == sub);
    }
}
