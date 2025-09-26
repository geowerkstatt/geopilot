using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Authorization handler for <see cref="GeopilotUserRequirement"/>.
/// </summary>
public class GeopilotUserHandler : AuthorizationHandler<GeopilotUserRequirement>
{
    private readonly Context dbContext;
    private readonly ILogger<GeopilotUserHandler> logger;
    private readonly IGeopilotUserInfoService userInfoService;
    private readonly IHttpContextAccessor httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopilotUserHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger used for authorization related logging.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="userInfoService">Service for retrieving user information from the identity provider.</param>
    /// <param name="httpContextAccessor">HTTP context accessor.</param>
    public GeopilotUserHandler(
        ILogger<GeopilotUserHandler> logger,
        Context dbContext,
        IGeopilotUserInfoService userInfoService,
        IHttpContextAccessor httpContextAccessor)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.userInfoService = userInfoService;
        this.httpContextAccessor = httpContextAccessor;
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
        var accessToken = ExtractAccessToken();
        if (string.IsNullOrEmpty(accessToken))
        {
            logger.LogError("No access token found in request.");
            return null;
        }

        var userInfo = await userInfoService.GetUserInfoAsync(accessToken);
        if (userInfo == null)
            return null;

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.AuthIdentifier == userInfo.Sub);
        if (user == null)
        {
            user = new User
            {
                AuthIdentifier = userInfo.Sub,
                Email = userInfo.Email,
                FullName = userInfo.Name,
            };

            // Elevate first user to admin
            if (!dbContext.Users.Any())
            {
                user.IsAdmin = true;
            }

            await dbContext.Users.AddAsync(user);
            logger.LogInformation("New user (with sub <{Sub}>) has been registered in database.", userInfo.Sub);
        }
        else if (user.Email != userInfo.Email || user.FullName != userInfo.Name)
        {
            // Update user information in database from userinfo response
            user.Email = userInfo.Email;
            user.FullName = userInfo.Name;
        }

        await dbContext.SaveChangesAsync();
        return await dbContext.Users.SingleAsync(u => u.AuthIdentifier == userInfo.Sub);
    }

    private string? ExtractAccessToken()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null) return null;

        var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return authHeader.Substring("Bearer ".Length);
        }

        return httpContext.Request.Cookies["geopilot.auth"];
    }
}
