using Geopilot.Api.Models;
using IdentityModel;
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
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GeopilotUserRequirement requirement)
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
        var contextUser = context.User;

        var sub = context.User.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Sub)?.Value;
        var email = context.User.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Email)?.Value;
        var name = context.User.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Name)?.Value;
        var clientId = contextUser.Claims.FirstOrDefault(claim => claim.Type == JwtClaimTypes.ClientId)?.Value;

        var isHuman = !string.IsNullOrEmpty(email);
        var loginId = isHuman ? email! : clientId;
        if (loginId == null)
            throw new InvalidOperationException($"Cannot determine login identifier for sub={sub}");

        var fullName = isHuman ? name ?? email!.Split('@')[0] : clientId!;
        var userType = isHuman ? UserType.HUMAN : UserType.MACHINE;

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.AuthIdentifier == sub);

        if (user == null)
        {
            user = new User
            {
                AuthIdentifier = sub,
                LoginIdentifier = loginId,
                Email = email,
                FullName = fullName,
                UserType = userType,
                // first human to sign up becomes admin:
                IsAdmin = (userType == UserType.HUMAN) &&
                          !await dbContext.Users.AnyAsync(x => x.UserType == UserType.HUMAN)
            };
            dbContext.Users.Add(user);
            logger.LogInformation("Registered new {UserType} user {Sub}", userType, sub);
        }
        else
        {
            if (user.LoginIdentifier != loginId) user.LoginIdentifier = loginId;
            if (user.Email != email) user.Email = email;
            if (user.FullName != fullName) user.FullName = fullName;
            if (user.UserType != userType) user.UserType = userType;
        }

        await dbContext.SaveChangesAsync();
        return user;
    }
}
